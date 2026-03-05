using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements daemon startup readiness probing via repeated ping attempts. </summary>
internal sealed class DaemonStartupReadinessProbe : IDaemonStartupReadinessProbe
{
    private const int ProbeRetryDelayMilliseconds = 100;

    private readonly IDaemonPingClient daemonPingClient;

    private readonly IDaemonLogReader daemonLogReader;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartupReadinessProbe" /> class. </summary>
    /// <param name="daemonPingClient"> The daemon ping client dependency. </param>
    /// <param name="daemonLogReader"> The daemon log-reader dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartupReadinessProbe (
        IDaemonPingClient daemonPingClient,
        IDaemonLogReader daemonLogReader)
    {
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.daemonLogReader = daemonLogReader ?? throw new ArgumentNullException(nameof(daemonLogReader));
    }

    /// <summary> Waits until daemon endpoint becomes reachable, or timeout expires. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The startup readiness timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The readiness probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartupReadinessProbeResult> WaitUntilReady (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var startAtUtc = DateTimeOffset.UtcNow;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await daemonPingClient.Ping(
                        unityProject,
                        timeout,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return DaemonStartupReadinessProbeResult.Ready();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
            {
                var startupFailureError = await TryResolveStartupFailureFromDaemonLog(
                        unityProject,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (startupFailureError is not null)
                {
                    return DaemonStartupReadinessProbeResult.Failure(startupFailureError);
                }

                var elapsed = DateTimeOffset.UtcNow - startAtUtc;
                if (elapsed >= timeout)
                {
                    return DaemonStartupReadinessProbeResult.Failure(ExecutionError.Timeout(
                        $"Timed out while waiting for daemon startup. Timeout={timeout.TotalMilliseconds:0}ms."));
                }

                var retryDelayMilliseconds = Math.Min(
                    ProbeRetryDelayMilliseconds,
                    Math.Max(1, (int)(timeout - elapsed).TotalMilliseconds));
                await Task.Delay(retryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return DaemonStartupReadinessProbeResult.Failure(ExecutionError.InternalError(
                    $"Failed while probing daemon startup readiness. {exception.Message}"));
            }
        }
    }

    private async ValueTask<ExecutionError?> TryResolveStartupFailureFromDaemonLog (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        var logReadResult = await daemonLogReader.ReadTail(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!logReadResult.IsSuccess || string.IsNullOrWhiteSpace(logReadResult.Text))
        {
            return null;
        }

        if (TryGetCompilerErrorSummary(logReadResult.Text, out var compilerErrorSummary))
        {
            return ExecutionError.InternalError(
                $"Unity daemon startup failed because scripts have compiler errors. {compilerErrorSummary}");
        }

        return null;
    }

    private static bool TryGetCompilerErrorSummary (
        string logText,
        out string summary)
    {
        const string compilerErrorsMarker = "Scripts have compiler errors";
        summary = string.Empty;

        var lines = logText.Split('\n');
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length == 0)
            {
                continue;
            }

            if (trimmedLine.Contains("error CS", StringComparison.OrdinalIgnoreCase))
            {
                summary = $"FirstError={trimmedLine}";
                return true;
            }

            if (trimmedLine.Contains(compilerErrorsMarker, StringComparison.OrdinalIgnoreCase))
            {
                summary = $"Marker={trimmedLine}";
                return true;
            }
        }

        return false;
    }
}