using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Waits for daemon lifecycle readiness and dispatches readiness-gated daemon requests. </summary>
internal sealed class UnityDaemonReadinessGate
{
    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityDaemonReadinessGate" /> class. </summary>
    /// <param name="daemonPingInfoClient"> The daemon ping client that returns lifecycle payloads. </param>
    /// <param name="timeProvider"> The time provider used for retry delays. </param>
    public UnityDaemonReadinessGate (
        IDaemonPingInfoClient daemonPingInfoClient,
        TimeProvider? timeProvider = null)
    {
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Tries to read readiness gate settings from one dispatch request. </summary>
    /// <param name="dispatchRequest"> The dispatch request. </param>
    /// <param name="opsReadRequest"> The parsed ops.read request when present; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the request is a readiness-gated <c>ops.read</c> request; otherwise <see langword="false" />. </returns>
    public bool TryReadReadinessGatedOpsRead (
        UnityIpcDispatchRequest dispatchRequest,
        out IpcOpsReadRequest? opsReadRequest)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);

        opsReadRequest = null;
        if (!string.Equals(dispatchRequest.Method, IpcMethodNames.OpsRead, StringComparison.Ordinal))
        {
            return false;
        }

        if (!IpcPayloadCodec.TryDeserialize(dispatchRequest.Payload, out IpcOpsReadRequest parsedPayload, out _))
        {
            throw new InvalidOperationException("ops.read payload must be valid before Unity IPC request execution begins.");
        }

        if (!parsedPayload.RequireReadinessGate)
        {
            return false;
        }

        opsReadRequest = parsedPayload;
        return true;
    }

    /// <summary> Dispatches one daemon request after readiness gate checks. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="dispatchRequest"> The original IPC dispatch request. </param>
    /// <param name="opsReadRequest"> The parsed ops.read request payload. </param>
    /// <param name="budget"> The shared execution timeout budget. </param>
    /// <param name="daemonIpcClient"> The daemon IPC client. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Unity request execution result. </returns>
    public async ValueTask<UnityRequestExecutionResult> ExecuteAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        IpcOpsReadRequest opsReadRequest,
        UnityIpcExecutionBudget budget,
        IUnityIpcClient daemonIpcClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(opsReadRequest);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(daemonIpcClient);
        cancellationToken.ThrowIfCancellationRequested();

        while (true)
        {
            var readinessFailure = await WaitUntilReadyAsync(
                    unityProject,
                    opsReadRequest.FailFast,
                    budget,
                    cancellationToken)
                .ConfigureAwait(false);
            if (readinessFailure != null)
            {
                return UnityRequestExecutionResult.Failure(readinessFailure);
            }

            var failFastDispatchRequest = CreateFailFastDispatchRequest(dispatchRequest, opsReadRequest);
            if (!budget.TryGetRemainingTimeout(out var requestTimeout))
            {
                return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.Timeout(
                    "Timed out before Unity IPC request dispatch could begin."));
            }

            var dispatchResult = await daemonIpcClient.SendAsync(
                    unityProject,
                    failFastDispatchRequest,
                    requestTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!UnityDaemonReadinessPolicy.ShouldRetryAfterLateWaitableRegression(
                    dispatchResult,
                    opsReadRequest.FailFast))
            {
                return dispatchResult;
            }
        }
    }

    private async ValueTask<UnityRequestFailure?> WaitUntilReadyAsync (
        ResolvedUnityProjectContext unityProject,
        bool failFast,
        UnityIpcExecutionBudget budget,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!budget.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return CreateDaemonTimeoutFailure(budget.Timeout);
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
                var readinessDecision = UnityDaemonReadinessPolicy.Evaluate(pingResponse, failFast);
                if (readinessDecision.IsReady)
                {
                    return null;
                }

                if (readinessDecision.IsFailure)
                {
                    return UnityIpcFailureClassifier.FromCodeAndMessage(
                        readinessDecision.ErrorCode!.Value,
                        readinessDecision.ErrorMessage!);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException)
            {
            }
            catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
            {
                return UnityIpcFailureClassifier.DaemonNotRunning(exception);
            }
            catch (Exception exception)
            {
                return UnityIpcFailureClassifier.InternalError(
                    $"Failed while waiting for Unity daemon readiness. {exception.Message}");
            }

            if (!budget.TryGetRemainingTimeout(out remainingTimeout))
            {
                return CreateDaemonTimeoutFailure(budget.Timeout);
            }

            await TimeProviderDelay.DelayAsync(
                    GetReadinessRetryDelay(remainingTimeout),
                    timeProvider,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static UnityIpcDispatchRequest CreateFailFastDispatchRequest (
        UnityIpcDispatchRequest dispatchRequest,
        IpcOpsReadRequest opsReadRequest)
    {
        // NOTE:
        // Daemon-side ops.read readiness waits must not hold the shared IPC request loop open,
        // otherwise status/log/shutdown requests can be starved behind one long-lived wait.
        // Keep one final fail-fast gate on the dispatched request so the handler still rejects
        // lifecycle regressions that happen after the last client-side readiness probe.
        var payload = IpcPayloadCodec.SerializeToElement(opsReadRequest with
        {
            FailFast = true,
        });

        return new UnityIpcDispatchRequest(dispatchRequest.Method, payload);
    }

    private static UnityRequestFailure CreateDaemonTimeoutFailure (TimeSpan timeout)
    {
        return UnityIpcFailureClassifier.Timeout(
            $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.");
    }

    private static TimeSpan GetReadinessRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }
}
