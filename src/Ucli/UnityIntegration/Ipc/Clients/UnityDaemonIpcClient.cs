using System.Runtime.ExceptionServices;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;
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

    private readonly DaemonSessionAcquisitionCoordinator sessionAcquisitionCoordinator;

    /// <summary> Initializes a new instance of the <see cref="UnityDaemonIpcClient" /> class. </summary>
    /// <param name="transportClient"> The shared transport client dependency. </param>
    /// <param name="sessionAcquisitionCoordinator"> The coordinator that creates one acquisition scope per logical request. </param>
    public UnityDaemonIpcClient (
        IIpcTransportClient transportClient,
        DaemonSessionAcquisitionCoordinator sessionAcquisitionCoordinator)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.sessionAcquisitionCoordinator = sessionAcquisitionCoordinator ?? throw new ArgumentNullException(nameof(sessionAcquisitionCoordinator));
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
        IpcResponse? sessionTokenRejection = null;
        Exception? firstResponseInterruption = null;
        var responseReplayPolicy = dispatchRequest.ResponseReplayPolicy;
        var acquisitionScope = sessionAcquisitionCoordinator.CreateScope(deadline);
        var sessionAcquisition = await acquisitionScope.ResolveCurrentAsync(
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);

        while (true)
        {
            switch (sessionAcquisition.Kind)
            {
                case DaemonSessionAcquisitionKind.Success:
                    break;
                case DaemonSessionAcquisitionKind.RequestDeadlineExpired:
                    return firstResponseInterruption is null
                        ? CreateDeadlineExceededResult(deadline.Timeout)
                        : CreateResponseInterruptionResult(firstResponseInterruption, deadline.Timeout);
                case DaemonSessionAcquisitionKind.PublicationWindowExpired:
                case DaemonSessionAcquisitionKind.SessionNotAvailable:
                    if (firstResponseInterruption is not null)
                    {
                        return CreateResponseInterruptionResult(firstResponseInterruption, deadline.Timeout);
                    }

                    return sessionTokenRejection is not null
                        ? UnityRequestExecutionResult.Success(UnityRequestResponseFactory.Create(sessionTokenRejection))
                        : UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.FromCodeAndMessage(
                            UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                            DaemonSessionAcquisitionResult.SessionNotAvailableMessage));
                case DaemonSessionAcquisitionKind.EndpointAvailabilityWindowExpired:
                    return firstResponseInterruption is null
                        ? UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.FromCodeAndMessage(
                            UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                            DaemonSessionAcquisitionResult.SessionNotAvailableMessage))
                        : CreateResponseInterruptionResult(firstResponseInterruption, deadline.Timeout);
                case DaemonSessionAcquisitionKind.HostIdentityMismatch:
                    if (firstResponseInterruption is null)
                    {
                        throw new InvalidOperationException(
                            "A durable response replay host mismatch requires the original response interruption.");
                    }

                    return CreateResponseInterruptionResult(firstResponseInterruption, deadline.Timeout);
                case DaemonSessionAcquisitionKind.SessionReadFailure:
                    if (firstResponseInterruption is not null)
                    {
                        return CreateResponseInterruptionResult(firstResponseInterruption, deadline.Timeout);
                    }

                    return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.InternalError(
                        $"Daemon session could not be read. {sessionAcquisition.ReadFailure!.Error!.Message}"));
                default:
                    throw new InvalidOperationException(
                        $"Unsupported daemon session acquisition outcome: {sessionAcquisition.Kind}.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return firstResponseInterruption is null
                    ? CreateDeadlineExceededResult(deadline.Timeout)
                    : CreateResponseInterruptionResult(firstResponseInterruption, deadline.Timeout);
            }

            if (!deadline.TryGetRemainingMilliseconds(out var remainingMilliseconds))
            {
                return firstResponseInterruption is null
                    ? CreateDeadlineExceededResult(deadline.Timeout)
                    : CreateResponseInterruptionResult(firstResponseInterruption, deadline.Timeout);
            }

            var session = sessionAcquisition.Session!;
            try
            {
                var response = await sendAttempt(
                        session.Endpoint,
                        UnityIpcRequestFactory.Create(
                            session.SessionToken,
                            dispatchRequest.Method,
                            dispatchRequest.Payload,
                            requestId,
                            responseMode,
                            deadline.UtcDeadline,
                            remainingMilliseconds),
                        remainingTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (IsSessionTokenInvalid(response))
                {
                    sessionTokenRejection = response;
                    if (firstResponseInterruption is not null
                        && responseReplayPolicy == UnityIpcResponseReplayPolicy.DurableSameHostSuccessor)
                    {
                        sessionAcquisition = await acquisitionScope.ResolveDurableReplacementAsync(
                                unityProject,
                                session,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        firstResponseInterruption = null;
                        sessionAcquisition = await acquisitionScope.ResolveReplacementAsync(
                                unityProject,
                                session,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    continue;
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
                var isRetryableBeforeRequestWrite = DaemonIpcConnectionFailureClassifier
                    .IsRetryableBeforeRequestWrite(exception);
                if (sessionTokenRejection is not null
                    && firstResponseInterruption is null
                    && !isRetryableBeforeRequestWrite
                    && (responseReplayPolicy == UnityIpcResponseReplayPolicy.None
                        || !IsRecoverableResponseInterruption(exception)))
                {
                    return UnityRequestExecutionResult.Failure(
                        UnityIpcFailureClassifier.FromDaemonDispatchException(exception, remainingTimeout));
                }

                if (isRetryableBeforeRequestWrite)
                {
                    if (firstResponseInterruption is not null
                        && responseReplayPolicy == UnityIpcResponseReplayPolicy.DurableSameHostSuccessor)
                    {
                        sessionAcquisition = await acquisitionScope.ResolveAfterDurablePreWriteFailureAsync(
                                unityProject,
                                session,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        firstResponseInterruption = null;
                        sessionAcquisition = await acquisitionScope.ResolveAfterPreWriteFailureAsync(
                                unityProject,
                                session,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    continue;
                }

                if (responseReplayPolicy != UnityIpcResponseReplayPolicy.None
                    && IsRecoverableResponseInterruption(exception))
                {
                    firstResponseInterruption ??= exception;
                    if (!deadline.IsExpired)
                    {
                        sessionAcquisition = responseReplayPolicy switch
                        {
                            UnityIpcResponseReplayPolicy.StatelessAnyHostSuccessor =>
                                await acquisitionScope.ResolveAfterStatelessResponseInterruptionAsync(
                                        unityProject,
                                        session,
                                        cancellationToken)
                                    .ConfigureAwait(false),
                            UnityIpcResponseReplayPolicy.DurableSameHostSuccessor =>
                                await acquisitionScope.ResolveAfterDurableResponseInterruptionAsync(
                                        unityProject,
                                        session,
                                        cancellationToken)
                                    .ConfigureAwait(false),
                            _ => throw new InvalidOperationException(
                                $"Unsupported IPC response replay policy: {responseReplayPolicy}."),
                        };
                        continue;
                    }
                }

                if (firstResponseInterruption is not null
                    && responseReplayPolicy == UnityIpcResponseReplayPolicy.DurableSameHostSuccessor)
                {
                    return CreateResponseInterruptionResult(firstResponseInterruption, deadline.Timeout);
                }

                if (deadline.IsExpired)
                {
                    return firstResponseInterruption is null
                        ? CreateDeadlineExceededResult(deadline.Timeout)
                        : CreateResponseInterruptionResult(firstResponseInterruption, deadline.Timeout);
                }

                return UnityRequestExecutionResult.Failure(
                    UnityIpcFailureClassifier.FromDaemonDispatchException(exception, remainingTimeout));
            }
        }
    }

    private static UnityRequestExecutionResult CreateDeadlineExceededResult (TimeSpan timeout)
    {
        return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.Timeout(
            $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
    }

    private static UnityRequestExecutionResult CreateResponseInterruptionResult (
        Exception exception,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var failure = exception is TimeoutException
            ? UnityIpcFailureClassifier.Timeout(
                $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds. {exception.Message}")
            : UnityIpcFailureClassifier.FromDaemonDispatchException(exception, timeout);
        return UnityRequestExecutionResult.Failure(failure);
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

}
