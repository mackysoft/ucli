using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.Startup;

/// <summary> Implements daemon startup endpoint probing via repeated ping attempts. </summary>
internal sealed class DaemonStartupReadinessProbe : IDaemonStartupReadinessProbe
{
    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IUnityLogReader unityLogReader;

    private readonly IUnityProjectLockPreflightService unityProjectLockPreflightService;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartupReadinessProbe" /> class. </summary>
    /// <param name="daemonPingInfoClient"> The daemon ping-info client dependency. </param>
    /// <param name="unityLogReader"> The Unity log-reader dependency. </param>
    /// <param name="unityProjectLockPreflightService"> The Unity project lock preflight service dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting and retry delays. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartupReadinessProbe (
        IDaemonPingInfoClient daemonPingInfoClient,
        IUnityLogReader unityLogReader,
        IUnityProjectLockPreflightService unityProjectLockPreflightService,
        TimeProvider? timeProvider = null)
    {
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.unityLogReader = unityLogReader ?? throw new ArgumentNullException(nameof(unityLogReader));
        this.unityProjectLockPreflightService = unityProjectLockPreflightService ?? throw new ArgumentNullException(nameof(unityProjectLockPreflightService));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Waits until daemon startup accepts execution requests, or fails when timeout expires or startup reaches one non-waitable lifecycle state. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The startup readiness timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The readiness probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartupReadinessProbeResult> WaitUntilReadyAsync (
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

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (daemonProcessId is int processId && !ProcessLivenessProbe.IsAlive(processId))
            {
                var postExitLockDiagnostic = await CreatePostExitLockDiagnosticAsync(
                        unityProject,
                        cancellationToken)
                    .ConfigureAwait(false);
                var startupFailure = await TryClassifyStartupFailureAsync(
                        unityProject,
                        includeProjectLockFile: false,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (startupFailure.Error is not null)
                {
                    return DaemonStartupReadinessProbeResult.Failure(
                        AppendDiagnostic(startupFailure.Error, postExitLockDiagnostic),
                        startupFailure.Classification);
                }

                return DaemonStartupReadinessProbeResult.Failure(ExecutionError.InternalError(
                    AppendDiagnostic(
                        $"Unity daemon process exited before startup readiness was confirmed. ProcessId={processId}.",
                        postExitLockDiagnostic)));
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
                var pingResponse = await daemonPingInfoClient.PingAndReadAsync(
                        unityProject,
                        attemptTimeout,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return DaemonStartupReadinessProbeResult.Ready(pingResponse);
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

                await TimeProviderDelay.DelayAsync(GetRetryDelay(remainingTimeout), timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
            {
                // NOTE:
                // A Unity process launched by uCLI creates Temp/UnityLockfile before its IPC server
                // is ready. Treat that lock as external only when no launched process id is known.
                var startupFailure = await TryClassifyStartupFailureAsync(
                        unityProject,
                        includeProjectLockFile: daemonProcessId is null,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (startupFailure.Error is not null)
                {
                    return DaemonStartupReadinessProbeResult.Failure(
                        startupFailure.Error,
                        startupFailure.Classification);
                }

                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return DaemonStartupReadinessProbeResult.Failure(ExecutionError.Timeout(
                        $"Timed out while waiting for daemon startup. Timeout={timeout.TotalMilliseconds:0}ms."));
                }

                await TimeProviderDelay.DelayAsync(GetRetryDelay(remainingTimeout), timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return DaemonStartupReadinessProbeResult.Failure(ExecutionError.InternalError(
                    $"Failed while probing daemon startup readiness. {exception.Message}"));
            }
        }
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }

    private async ValueTask<StartupFailureClassificationResult> TryClassifyStartupFailureAsync (
        ResolvedUnityProjectContext unityProject,
        bool includeProjectLockFile,
        CancellationToken cancellationToken)
    {
        if (includeProjectLockFile)
        {
            var projectLockPreflightResult = await unityProjectLockPreflightService.PrepareForUnityProcessStartAsync(
                    unityProject,
                    cancellationToken)
                .ConfigureAwait(false);
            var projectLockError = UnityProjectLockPreflightErrorFactory.CreateLaunchBlockingError(
                unityProject,
                projectLockPreflightResult);
            if (projectLockError != null)
            {
                return new StartupFailureClassificationResult(projectLockError, null);
            }
        }

        var logReadResult = await unityLogReader.ReadTailAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!logReadResult.IsSuccess || string.IsNullOrWhiteSpace(logReadResult.Text))
        {
            return new StartupFailureClassificationResult(null, null);
        }

        var latestStartupLogText = DaemonStartupFailureLogClassifier.GetLatestStartupLogText(logReadResult.Text);
        if (!DaemonStartupFailureLogClassifier.TryClassifyFailure(
                latestStartupLogText,
                DaemonStartupFailureClassificationContext.Batchmode,
                out var classification))
        {
            return new StartupFailureClassificationResult(null, null);
        }

        return new StartupFailureClassificationResult(
            ExecutionError.InternalError(classification.Message, DaemonErrorCodes.DaemonStartupBlocked),
            classification);
    }

    private async ValueTask<string?> CreatePostExitLockDiagnosticAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        var preflightResult = await unityProjectLockPreflightService.CleanupStaleLockAfterUnityProcessExitAsync(
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);
        return UnityProjectLockPreflightErrorFactory.CreatePostExitDiagnostic(preflightResult);
    }

    private static ExecutionError AppendDiagnostic (
        ExecutionError error,
        string? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(error);
        return string.IsNullOrWhiteSpace(diagnostic)
            ? error
            : error with { Message = AppendDiagnostic(error.Message, diagnostic) };
    }

    private static string AppendDiagnostic (
        string message,
        string? diagnostic)
    {
        return string.IsNullOrWhiteSpace(diagnostic)
            ? message
            : $"{message} {diagnostic}";
    }

    private readonly record struct StartupFailureClassificationResult (
        ExecutionError? Error,
        DaemonStartupFailureClassification? Classification);
}
