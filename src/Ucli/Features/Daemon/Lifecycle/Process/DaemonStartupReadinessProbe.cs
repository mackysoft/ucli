using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process;

/// <summary> Implements daemon startup readiness probing via repeated ping attempts. </summary>
internal sealed class DaemonStartupReadinessProbe : IDaemonStartupReadinessProbe
{
    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IUnityLogReader unityLogReader;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartupReadinessProbe" /> class. </summary>
    /// <param name="daemonPingInfoClient"> The daemon ping-info client dependency. </param>
    /// <param name="unityLogReader"> The Unity log-reader dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartupReadinessProbe (
        IDaemonPingInfoClient daemonPingInfoClient,
        IUnityLogReader unityLogReader)
    {
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.unityLogReader = unityLogReader ?? throw new ArgumentNullException(nameof(unityLogReader));
    }

    /// <summary> Waits until daemon startup accepts execution requests, or fails when timeout expires or startup reaches one non-waitable lifecycle state. </summary>
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
            if (daemonProcessId is int processId && !ProcessLivenessProbe.IsAlive(processId))
            {
                var startupFailureError = await TryClassifyStartupFailureFromLatestLogText(
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
                var pingResponse = await daemonPingInfoClient.PingAndRead(
                        unityProject,
                        attemptTimeout,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (pingResponse.CanAcceptExecutionRequests)
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
                var startupFailureError = await TryClassifyStartupFailureFromLatestLogText(
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
            IpcEditorLifecycleStateCodec.BlockedByModal =>
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
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Busy, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Compiling, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.DomainReloading, StringComparison.Ordinal);
    }

    private async ValueTask<ExecutionError?> TryClassifyStartupFailureFromLatestLogText (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        var logReadResult = await unityLogReader.ReadTail(
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
}