using System.Runtime.ExceptionServices;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary> Sends one IPC request through the running Unity daemon. </summary>
internal sealed class UnityDaemonIpcClient : IUnityIpcClient
{
    private readonly IIpcTransportClient transportClient;

    private readonly IDaemonSessionConnectionProvider daemonSessionConnectionProvider;

    private readonly UnityDaemonRecoveryWaiter? recoveryWaiter;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityDaemonIpcClient" /> class. </summary>
    /// <param name="transportClient"> The shared transport client dependency. </param>
    /// <param name="daemonSessionConnectionProvider"> The daemon session connection provider dependency. </param>
    /// <param name="recoveryWaiter"> The daemon lifecycle recovery waiter dependency. </param>
    /// <param name="timeProvider"> The time provider used for retry deadlines and delays. </param>
    public UnityDaemonIpcClient (
        IIpcTransportClient transportClient,
        IDaemonSessionConnectionProvider daemonSessionConnectionProvider,
        UnityDaemonRecoveryWaiter? recoveryWaiter,
        TimeProvider timeProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.daemonSessionConnectionProvider = daemonSessionConnectionProvider ?? throw new ArgumentNullException(nameof(daemonSessionConnectionProvider));
        this.recoveryWaiter = recoveryWaiter;
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public UnityExecutionTarget Target => UnityExecutionTarget.Daemon;

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(deadline);
        return await SendCoreAsync(
                unityProject,
                dispatchRequest,
                deadline,
                IpcResponseMode.Single,
                (endpoint, request, attemptTimeout, token) => transportClient.SendAsync(
                    endpoint,
                    request,
                    attemptTimeout,
                    token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> SendStreamingAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(deadline);
        if (!UnityIpcMethodCapabilities.SupportsStreaming(dispatchRequest.Method))
        {
            return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.InternalError(
                $"IPC method does not support streaming: {ContractLiteralCodec.ToValue(dispatchRequest.Method)}."));
        }

        return await SendCoreAsync(
                unityProject,
                dispatchRequest,
                deadline,
                IpcResponseMode.Stream,
                (endpoint, request, attemptTimeout, token) => transportClient.SendStreamingAsync(
                    endpoint,
                    request,
                    attemptTimeout,
                    onProgressFrame,
                    token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<UnityRequestExecutionResult> SendCoreAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        IpcResponseMode responseMode,
        Func<IpcEndpoint, IpcRequestEnvelope, TimeSpan, CancellationToken, ValueTask<IpcResponse>> sendAttempt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var requestId = Guid.NewGuid();
        ExecutionDeadline? endpointAbsenceRetryDeadline = null;
        ExecutionDeadline? sessionPublicationRetryDeadline = null;
        IpcSessionToken? rejectedSessionToken = null;
        IpcResponse? sessionTokenRejection = null;

        while (true)
        {
            var sessionConnectionResolution = await ResolveSessionConnectionBeforeDeadlineAsync(
                    unityProject,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (sessionConnectionResolution.DeadlineExpired)
            {
                return CreateDeadlineExceededResult(deadline.Timeout);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return CreateDeadlineExceededResult(deadline.Timeout);
            }

            if (!deadline.TryGetRemainingMilliseconds(out var remainingMilliseconds))
            {
                return CreateDeadlineExceededResult(deadline.Timeout);
            }

            var sessionConnectionResult = sessionConnectionResolution.ConnectionResult!;
            if (!sessionConnectionResult.IsSuccess)
            {
                if (rejectedSessionToken is not null
                    && sessionConnectionResult.IsSessionNotAvailable)
                {
                    var publicationRetryDecision = await DelayForSessionPublicationAsync(
                            unityProject,
                            deadline,
                            sessionPublicationRetryDeadline,
                            cancellationToken)
                        .ConfigureAwait(false);
                    sessionPublicationRetryDeadline = publicationRetryDecision.SessionPublicationRetryDeadline;
                    if (publicationRetryDecision.ShouldRetry)
                    {
                        continue;
                    }

                    if (deadline.IsExpired)
                    {
                        return CreateDeadlineExceededResult(deadline.Timeout);
                    }

                    return UnityRequestExecutionResult.Success(
                        UnityRequestResponseFactory.Create(sessionTokenRejection!));
                }

                var recoveryDelayConsumed = recoveryWaiter != null
                    && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false);

                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return CreateDeadlineExceededResult(deadline.Timeout);
                }

                if (recoveryDelayConsumed)
                {
                    continue;
                }

                if (sessionConnectionResult.IsSessionNotAvailable)
                {
                    return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.FromCodeAndMessage(
                        UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                        DaemonSessionConnectionResolutionResult.SessionNotAvailableMessage));
                }

                return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.InternalError(
                    $"Daemon session connection could not be resolved. {sessionConnectionResult.Error!.Message}"));
            }

            try
            {
                var sessionConnection = sessionConnectionResult.Connection!;
                if (rejectedSessionToken is not null
                    && sessionConnection.SessionToken.Equals(rejectedSessionToken))
                {
                    var publicationRetryDecision = await DelayForSessionPublicationAsync(
                            unityProject,
                            deadline,
                            sessionPublicationRetryDeadline,
                            cancellationToken)
                        .ConfigureAwait(false);
                    sessionPublicationRetryDeadline = publicationRetryDecision.SessionPublicationRetryDeadline;
                    if (publicationRetryDecision.ShouldRetry)
                    {
                        continue;
                    }

                    if (deadline.IsExpired)
                    {
                        return CreateDeadlineExceededResult(deadline.Timeout);
                    }

                    return UnityRequestExecutionResult.Success(
                        UnityRequestResponseFactory.Create(sessionTokenRejection!));
                }

                var response = await sendAttempt(
                        sessionConnection.Endpoint,
                        UnityIpcRequestFactory.Create(
                            sessionConnection.SessionToken,
                            dispatchRequest.Method,
                            dispatchRequest.Payload,
                            requestId,
                            responseMode,
                            deadline.UtcDeadline,
                            remainingMilliseconds),
                        remainingTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (IsSessionTokenInvalid(response) && rejectedSessionToken is null)
                {
                    rejectedSessionToken = sessionConnection.SessionToken;
                    sessionTokenRejection = response;
                    var retryDecision = await DelayForSessionPublicationAsync(
                            unityProject,
                            deadline,
                            sessionPublicationRetryDeadline,
                            cancellationToken)
                        .ConfigureAwait(false);
                    sessionPublicationRetryDeadline = retryDecision.SessionPublicationRetryDeadline;
                    if (retryDecision.ShouldRetry)
                    {
                        continue;
                    }

                    if (deadline.IsExpired)
                    {
                        return CreateDeadlineExceededResult(deadline.Timeout);
                    }
                }

                return UnityRequestExecutionResult.Success(UnityRequestResponseFactory.Create(response));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (IpcProgressFrameHandlerException exception)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException!).Throw();
                throw;
            }
            catch (Exception exception)
            {
                if (rejectedSessionToken is not null
                    && (!dispatchRequest.IsRecoverable
                        || !IsRecoverableResponseInterruption(exception)))
                {
                    return UnityRequestExecutionResult.Failure(
                        UnityIpcFailureClassifier.FromDaemonDispatchException(exception, remainingTimeout));
                }

                var shouldRetry = false;
                if (dispatchRequest.IsRecoverable)
                {
                    var retryDecision = await ShouldRetryRecoverableDispatchAsync(
                            unityProject,
                            deadline,
                            endpointAbsenceRetryDeadline,
                            exception,
                            cancellationToken)
                        .ConfigureAwait(false);
                    endpointAbsenceRetryDeadline = retryDecision.EndpointAbsenceRetryDeadline;
                    shouldRetry = retryDecision.ShouldRetry;
                }
                else
                {
                    var retryDecision = await ShouldRetryEndpointAbsenceAsync(
                            unityProject,
                            deadline,
                            endpointAbsenceRetryDeadline,
                            exception,
                            cancellationToken)
                        .ConfigureAwait(false);
                    endpointAbsenceRetryDeadline = retryDecision.EndpointAbsenceRetryDeadline;
                    shouldRetry = retryDecision.ShouldRetry;
                }

                if (shouldRetry)
                {
                    continue;
                }

                if (deadline.IsExpired)
                {
                    return CreateDeadlineExceededResult(deadline.Timeout);
                }

                return UnityRequestExecutionResult.Failure(
                    UnityIpcFailureClassifier.FromDaemonDispatchException(exception, remainingTimeout));
            }
        }
    }

    private async ValueTask<(DaemonSessionConnectionResolutionResult? ConnectionResult, bool DeadlineExpired)> ResolveSessionConnectionBeforeDeadlineAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var resolutionOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before daemon session resolution could begin.",
                "Timed out while resolving daemon session.",
                token => daemonSessionConnectionProvider.ResolveAsync(unityProject, token))
            .ConfigureAwait(false);
        if (!resolutionOperation.IsSuccess)
        {
            return (null, true);
        }

        return (resolutionOperation.Value!, false);
    }

    private static UnityRequestExecutionResult CreateDeadlineExceededResult (TimeSpan timeout)
    {
        return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.Timeout(
            $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
    }

    private async ValueTask<(bool ShouldRetry, ExecutionDeadline? EndpointAbsenceRetryDeadline)> ShouldRetryRecoverableDispatchAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        ExecutionDeadline? endpointAbsenceRetryDeadline,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        var recoveryDelayConsumed = recoveryWaiter != null
            && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false);
        if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        if (recoveryDelayConsumed)
        {
            return (true, endpointAbsenceRetryDeadline);
        }

        if (DaemonIpcConnectionFailureClassifier.IsRetryableBeforeRequestWrite(exception))
        {
            return await DelayWithinEndpointAbsenceGraceAsync(
                    deadline,
                    endpointAbsenceRetryDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!IsRecoverableResponseInterruption(exception))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        await TimeProviderDelay.DelayAsync(
                GetRetryDelay(remainingTimeout),
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false);
        return (true, endpointAbsenceRetryDeadline);
    }

    private async ValueTask<(bool ShouldRetry, ExecutionDeadline? EndpointAbsenceRetryDeadline)> DelayForEndpointAvailabilityAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        ExecutionDeadline? endpointAbsenceRetryDeadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        var recoveryDelayConsumed = recoveryWaiter != null
            && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false);
        if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        if (recoveryDelayConsumed)
        {
            return (true, endpointAbsenceRetryDeadline);
        }

        return await DelayWithinEndpointAbsenceGraceAsync(
                deadline,
                endpointAbsenceRetryDeadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<(bool ShouldRetry, ExecutionDeadline? SessionPublicationRetryDeadline)> DelayForSessionPublicationAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        ExecutionDeadline? sessionPublicationRetryDeadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return (false, sessionPublicationRetryDeadline);
        }

        var recoveryDelayConsumed = recoveryWaiter != null
            && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false);
        if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
        {
            return (false, sessionPublicationRetryDeadline);
        }

        if (recoveryDelayConsumed)
        {
            return (true, sessionPublicationRetryDeadline);
        }

        var effectivePublicationRetryDeadline = sessionPublicationRetryDeadline
            ?? deadline.CreateCappedDeadline(DaemonTimeouts.SessionPublicationRetryTimeout);
        if (!effectivePublicationRetryDeadline.TryGetRemainingTimeout(out var publicationRemainingTimeout))
        {
            return (false, effectivePublicationRetryDeadline);
        }

        await TimeProviderDelay.DelayAsync(
                GetRetryDelay(GetShorterTimeout(remainingTimeout, publicationRemainingTimeout)),
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false);
        return (true, effectivePublicationRetryDeadline);
    }

    private async ValueTask<(bool ShouldRetry, ExecutionDeadline? EndpointAbsenceRetryDeadline)> DelayWithinEndpointAbsenceGraceAsync (
        ExecutionDeadline deadline,
        ExecutionDeadline? endpointAbsenceRetryDeadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        var effectiveEndpointAbsenceRetryDeadline = endpointAbsenceRetryDeadline
            ?? deadline.CreateCappedDeadline(GetEndpointAbsenceRetryTimeout(remainingTimeout));
        if (!effectiveEndpointAbsenceRetryDeadline.TryGetRemainingTimeout(out var endpointAbsenceRemainingTimeout))
        {
            return (false, effectiveEndpointAbsenceRetryDeadline);
        }

        await TimeProviderDelay.DelayAsync(
                GetRetryDelay(GetShorterTimeout(remainingTimeout, endpointAbsenceRemainingTimeout)),
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false);
        return (true, effectiveEndpointAbsenceRetryDeadline);
    }

    private async ValueTask<(bool ShouldRetry, ExecutionDeadline? EndpointAbsenceRetryDeadline)> ShouldRetryEndpointAbsenceAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        ExecutionDeadline? endpointAbsenceRetryDeadline,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!DaemonIpcConnectionFailureClassifier.IsRetryableBeforeRequestWrite(exception))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        return await DelayForEndpointAvailabilityAsync(
                unityProject,
                deadline,
                endpointAbsenceRetryDeadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsRecoverableResponseInterruption (Exception exception)
    {
        return exception is IpcResponseReadInterruptedException
            || exception is TimeoutException and not IpcConnectTimeoutException;
    }

    private static bool IsSessionTokenInvalid (IpcResponse response)
    {
        foreach (var error in response.Errors)
        {
            if (error.Code == IpcSessionErrorCodes.SessionTokenInvalid)
            {
                return true;
            }
        }

        return false;
    }

    private static TimeSpan GetEndpointAbsenceRetryTimeout (TimeSpan remainingTimeout)
    {
        return GetShorterTimeout(remainingTimeout, DaemonTimeouts.ProbeAttemptTimeoutCap);
    }

    private static TimeSpan GetShorterTimeout (
        TimeSpan first,
        TimeSpan second)
    {
        return first < second ? first : second;
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(100, Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }
}
