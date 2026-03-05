using System.Diagnostics;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements daemon startup readiness probing via repeated ping attempts. </summary>
internal sealed class DaemonStartupReadinessProbe : IDaemonStartupReadinessProbe
{
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
        int? daemonProcessId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        if (daemonProcessId is int pid && pid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(daemonProcessId), daemonProcessId, "Daemon process id must be greater than zero.");
        }

        var deadline = ExecutionDeadline.Start(timeout);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (daemonProcessId is int processId && IsProcessExited(processId))
            {
                var startupFailureError = await TryResolveStartupFailureFromDaemonLog(
                        unityProject,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (startupFailureError is not null)
                {
                    return DaemonStartupReadinessProbeResult.Failure(startupFailureError);
                }

                return DaemonStartupReadinessProbeResult.Failure(ExecutionError.InternalError(
                    $"Unity daemon process exited before startup readiness was confirmed. ProcessId={processId}."));
            }

            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return DaemonStartupReadinessProbeResult.Failure(ExecutionError.Timeout(
                    $"Timed out while waiting for daemon startup. Timeout={timeout.TotalMilliseconds:0}ms."));
            }

            var attemptTimeout = remainingTimeout < DaemonTimeouts.ProbeAttemptTimeoutCap
                ? remainingTimeout
                : DaemonTimeouts.ProbeAttemptTimeoutCap;
            try
            {
                await daemonPingClient.Ping(
                        unityProject,
                        attemptTimeout,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return DaemonStartupReadinessProbeResult.Ready();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException)
            {
                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return DaemonStartupReadinessProbeResult.Failure(ExecutionError.Timeout(
                        $"Timed out while waiting for daemon startup. Timeout={timeout.TotalMilliseconds:0}ms."));
                }

                await Task.Delay(GetRetryDelay(remainingTimeout), cancellationToken).ConfigureAwait(false);
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

                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return DaemonStartupReadinessProbeResult.Failure(ExecutionError.Timeout(
                        $"Timed out while waiting for daemon startup. Timeout={timeout.TotalMilliseconds:0}ms."));
                }

                await Task.Delay(GetRetryDelay(remainingTimeout), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return DaemonStartupReadinessProbeResult.Failure(ExecutionError.InternalError(
                    $"Failed while probing daemon startup readiness. {exception.Message}"));
            }
        }
    }

    private static bool IsProcessExited (int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            try
            {
                return process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
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

        var latestStartupLogText = GetLatestStartupLogText(logReadResult.Text);
        if (TryGetCompilerErrorSummary(latestStartupLogText, out var compilerErrorSummary))
        {
            return ExecutionError.InternalError(
                $"Unity daemon startup failed because scripts have compiler errors. {compilerErrorSummary}");
        }

        if (TryGetPackageResolutionErrorSummary(latestStartupLogText, out var packageErrorSummary))
        {
            return ExecutionError.InternalError(
                $"Unity daemon startup failed because package resolution failed. {packageErrorSummary}");
        }

        return null;
    }

    private static string GetLatestStartupLogText (string logText)
    {
        const string startupMarker = "COMMAND LINE ARGUMENTS:";
        var markerIndex = logText.LastIndexOf(startupMarker, StringComparison.Ordinal);
        return markerIndex >= 0
            ? logText[markerIndex..]
            : logText;
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

    private static bool TryGetPackageResolutionErrorSummary (
        string logText,
        out string summary)
    {
        const string packageFailureMarker = "An error occurred while resolving packages:";
        summary = string.Empty;

        var lines = logText.Split('\n');
        var markerFound = false;
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length == 0)
            {
                continue;
            }

            if (!markerFound)
            {
                if (trimmedLine.Contains(packageFailureMarker, StringComparison.OrdinalIgnoreCase))
                {
                    markerFound = true;
                    summary = $"Marker={trimmedLine}";
                }

                continue;
            }

            if (trimmedLine.StartsWith("Project has invalid dependencies:", StringComparison.OrdinalIgnoreCase))
            {
                summary = $"Marker={trimmedLine}";
                continue;
            }

            summary = $"FirstError={trimmedLine}";
            return true;
        }

        return markerFound;
    }
}