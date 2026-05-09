using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.Startup;

/// <summary> Implements daemon startup readiness probing via repeated ping attempts. </summary>
internal sealed class DaemonStartupReadinessProbe : IDaemonStartupReadinessProbe
{
    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IUnityLogReader unityLogReader;

    private readonly IUnityProjectLockPreflightService unityProjectLockPreflightService;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartupReadinessProbe" /> class. </summary>
    /// <param name="daemonPingInfoClient"> The daemon ping-info client dependency. </param>
    /// <param name="unityLogReader"> The Unity log-reader dependency. </param>
    /// <param name="unityProjectLockPreflightService"> The Unity project lock preflight service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartupReadinessProbe (
        IDaemonPingInfoClient daemonPingInfoClient,
        IUnityLogReader unityLogReader,
        IUnityProjectLockPreflightService unityProjectLockPreflightService)
    {
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.unityLogReader = unityLogReader ?? throw new ArgumentNullException(nameof(unityLogReader));
        this.unityProjectLockPreflightService = unityProjectLockPreflightService ?? throw new ArgumentNullException(nameof(unityProjectLockPreflightService));
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

        var deadline = ExecutionDeadline.Start(timeout);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (daemonProcessId is int processId && !ProcessLivenessProbe.IsAlive(processId))
            {
                var postExitLockDiagnostic = await CreatePostExitLockDiagnosticAsync(
                        unityProject,
                        cancellationToken)
                    .ConfigureAwait(false);
                var startupFailureError = await TryClassifyStartupFailureAsync(
                        unityProject,
                        includeProjectLockFile: false,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (startupFailureError is not null)
                {
                    return DaemonStartupReadinessProbeResult.Failure(AppendDiagnostic(startupFailureError, postExitLockDiagnostic));
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
                if (pingResponse.CanAcceptExecutionRequests
                    && string.Equals(pingResponse.LifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal))
                {
                    return DaemonStartupReadinessProbeResult.Ready();
                }

                if (TryResolveNonRetryableLifecycleFailure(pingResponse, out var startupLifecycleError))
                {
                    return DaemonStartupReadinessProbeResult.Failure(startupLifecycleError!);
                }

                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return DaemonStartupReadinessProbeResult.Failure(ExecutionError.Timeout(
                        $"Timed out while waiting for daemon startup. Timeout={timeout.TotalMilliseconds:0}ms."));
                }

                await Task.Delay(GetRetryDelay(remainingTimeout), cancellationToken).ConfigureAwait(false);
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
                // NOTE:
                // A Unity process launched by uCLI creates Temp/UnityLockfile before its IPC server
                // is ready. Treat that lock as external only when no launched process id is known.
                var startupFailureError = await TryClassifyStartupFailureAsync(
                        unityProject,
                        includeProjectLockFile: daemonProcessId is null,
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

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }

    private static bool TryResolveNonRetryableLifecycleFailure (
        IpcPingResponse pingResponse,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(pingResponse);

        if (!IpcEditorLifecycleStateCodec.TryParse(pingResponse.LifecycleState, out var lifecycleState))
        {
            error = ExecutionError.InternalError(
                $"Unity daemon startup probe returned unsupported lifecycleState '{pingResponse.LifecycleState}'.");
            return true;
        }

        if (string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal))
        {
            error = ExecutionError.InternalError(
                "Unity daemon startup probe returned lifecycleState=ready while canAcceptExecutionRequests=false.");
            return true;
        }

        if (IsWaitableLifecycleState(lifecycleState!))
        {
            error = null;
            return false;
        }

        var blockingReason = IpcEditorBlockingReasonCodec.TryParse(pingResponse.BlockingReason, out var normalizedBlockingReason)
            ? normalizedBlockingReason
            : null;
        error = ExecutionError.InternalError(CreateNonWaitableLifecycleMessage(lifecycleState!, blockingReason));
        return true;
    }

    private static string CreateNonWaitableLifecycleMessage (
        string lifecycleState,
        string? blockingReason)
    {
        var lifecycleDetails = blockingReason is null
            ? $"lifecycleState={lifecycleState}"
            : $"lifecycleState={lifecycleState}, blockingReason={blockingReason}";

        return lifecycleState switch
        {
            IpcEditorLifecycleStateCodec.DomainReloading =>
                $"Unity daemon startup cannot continue while {lifecycleDetails}. Retry after lifecycleState=ready.",
            IpcEditorLifecycleStateCodec.Playmode =>
                $"Unity daemon startup cannot continue while {lifecycleDetails}. Exit Play Mode and retry after lifecycleState=ready.",
            IpcEditorLifecycleStateCodec.ModalBlocked =>
                $"Unity daemon startup cannot continue while {lifecycleDetails}. Resolve the modal dialog and retry after lifecycleState=ready.",
            IpcEditorLifecycleStateCodec.SafeMode =>
                $"Unity daemon startup cannot continue while {lifecycleDetails}. Resolve compiler errors and retry after lifecycleState=ready.",
            IpcEditorLifecycleStateCodec.ShuttingDown =>
                $"Unity daemon startup cannot continue while {lifecycleDetails}. Start a new daemon after shutdown finishes.",
            _ =>
                $"Unity daemon startup cannot continue while {lifecycleDetails}.",
        };
    }

    private static bool IsWaitableLifecycleState (string lifecycleState)
    {
        return string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Starting, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Recovering, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Busy, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Compiling, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.DomainReloading, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Reimporting, StringComparison.Ordinal);
    }

    private async ValueTask<ExecutionError?> TryClassifyStartupFailureAsync (
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
                return projectLockError;
            }
        }

        var logReadResult = await unityLogReader.ReadTailAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!logReadResult.IsSuccess || string.IsNullOrWhiteSpace(logReadResult.Text))
        {
            return null;
        }

        var latestStartupLogText = DaemonStartupFailureLogClassifier.GetLatestStartupLogText(logReadResult.Text);
        return DaemonStartupFailureLogClassifier.TryClassify(latestStartupLogText, out var error)
            ? error
            : null;
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
}
