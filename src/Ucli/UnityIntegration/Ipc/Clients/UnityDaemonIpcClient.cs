using System.Runtime.ExceptionServices;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Acquisition;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Ipc;
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

    /// <summary> Fixes the currently published daemon session for one Lifecycle Execution start. </summary>
    internal async ValueTask<DaemonHostBindingResult> BindHostAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var acquisition = await sessionAcquisitionCoordinator
            .CreateScope(deadline)
            .ResolveCurrentAsync(unityProject, cancellationToken)
            .ConfigureAwait(false);
        return acquisition.Kind == DaemonSessionAcquisitionKind.Success
            ? DaemonHostBindingResult.Success(acquisition.Session!)
            : DaemonHostBindingResult.Rejected(
                UnityIpcFailureClassifier.FromCodeAndMessage(
                    UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                    DaemonSessionAcquisitionResult.SessionNotAvailableMessage));
    }

    /// <summary>
    /// Sends through the daemon session selected at bind time. Session recovery is allowed only
    /// through the acquisition scope's same-host successor policy.
    /// </summary>
    internal async ValueTask<UnityRequestExecutionResult> SendBoundAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        DaemonSession fixedSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fixedSession);
        var scope = sessionAcquisitionCoordinator.CreateScope(deadline);
        if (!scope.TryBindDurableHost(fixedSession))
        {
            throw new InvalidOperationException("A fixed daemon session must establish its host identity.");
        }

        return await LifecycleExecutionCallerWaitCoordinator.WaitAsync(
                unityProject,
                dispatchRequest,
                deadline,
                dispatchObservation => SendCoreAsync(
                    unityProject,
                    dispatchRequest,
                    deadline,
                    IpcResponseMode.Single,
                    (endpoint, request, attemptTimeout, token) => transportClient.SendAsync(
                        endpoint,
                        request,
                        attemptTimeout,
                        token),
                    dispatchObservation,
                    scope,
                    DaemonSessionAcquisitionResult.Success(fixedSession),
                    cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

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
        return await LifecycleExecutionCallerWaitCoordinator.WaitAsync(
                unityProject,
                dispatchRequest,
                deadline,
                dispatchObservation => SendCoreAsync(
                    unityProject,
                    dispatchRequest,
                    deadline,
                    IpcResponseMode.Single,
                    (endpoint, request, attemptTimeout, token) => transportClient.SendAsync(
                        endpoint,
                        request,
                        attemptTimeout,
                        token),
                    dispatchObservation,
                    initialAcquisitionScope: null,
                    initialSessionAcquisition: null,
                    cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<UnityIpcReconnectAttempt> TryReconnectAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        LifecycleExecutionStartBinding requiredStart,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(requiredStart);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();
        if (dispatchRequest.RequiredStart != requiredStart)
        {
            throw new ArgumentException(
                "Daemon reconnect requires the dispatch's authoritative start binding.",
                nameof(requiredStart));
        }
        if (ProcessLivenessProbe.ObserveIdentity(
                requiredStart.Host.Process)
            == ProcessIdentityObservation.ConfirmedExitedOrReplaced)
        {
            return UnityIpcReconnectAttempt.Owned(
                CreateConfirmedHostExitResult(
                    requiredStart,
                    lifecycleActionDispatched: false));
        }

        var acquisitionScope = sessionAcquisitionCoordinator.CreateScope(deadline);
        var sessionAcquisition = await acquisitionScope.ResolveCurrentAsync(
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (ProcessLivenessProbe.ObserveIdentity(
                requiredStart.Host.Process)
            == ProcessIdentityObservation.ConfirmedExitedOrReplaced)
        {
            return UnityIpcReconnectAttempt.Owned(
                CreateConfirmedHostExitResult(
                    requiredStart,
                    lifecycleActionDispatched: false));
        }
        if (sessionAcquisition.Kind != DaemonSessionAcquisitionKind.Success
            || !MatchesRequiredStartHost(
                sessionAcquisition.Session!,
                requiredStart))
        {
            return UnityIpcReconnectAttempt.NotOwned();
        }

        if (!acquisitionScope.TryBindDurableHost(sessionAcquisition.Session!))
        {
            return UnityIpcReconnectAttempt.NotOwned();
        }

        var result = await LifecycleExecutionCallerWaitCoordinator.WaitAsync(
                unityProject,
                dispatchRequest,
                deadline,
                dispatchObservation => SendCoreAsync(
                    unityProject,
                    dispatchRequest,
                    deadline,
                    IpcResponseMode.Single,
                    (endpoint, request, attemptTimeout, token) => transportClient.SendAsync(
                        endpoint,
                        request,
                        attemptTimeout,
                        token),
                    dispatchObservation,
                    acquisitionScope,
                    sessionAcquisition,
                    cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
        return UnityIpcReconnectAttempt.Owned(result);
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
                $"IPC method does not support streaming: {TextVocabulary.GetText(dispatchRequest.Method)}."));
        }

        return await LifecycleExecutionCallerWaitCoordinator.WaitAsync(
                unityProject,
                dispatchRequest,
                deadline,
                dispatchObservation => SendCoreAsync(
                    unityProject,
                    dispatchRequest,
                    deadline,
                    IpcResponseMode.Stream,
                    (endpoint, request, attemptTimeout, token) => transportClient.SendStreamingAsync(
                        endpoint,
                        request,
                        attemptTimeout,
                        cancellationToken.IsCancellationRequested
                            ? static (_, _) => ValueTask.CompletedTask
                            : onProgressFrame,
                        token),
                    dispatchObservation,
                    initialAcquisitionScope: null,
                    initialSessionAcquisition: null,
                    cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<UnityRequestExecutionResult> SendCoreAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        IpcResponseMode responseMode,
        Func<IpcTransportEndpoint, IpcRequestEnvelope, TimeSpan, CancellationToken, ValueTask<IpcResponse>> sendAttempt,
        LifecycleExecutionDispatchObservation? dispatchObservation,
        DaemonSessionAcquisitionScope? initialAcquisitionScope,
        DaemonSessionAcquisitionResult? initialSessionAcquisition,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var dispatchCancellationToken = cancellationToken;
        var requestId = Guid.NewGuid();
        var lifecycleStartRequestId = Guid.NewGuid();
        IpcResponse? sessionTokenRejection = null;
        Exception? firstResponseInterruption = null;
        LifecycleExecutionStartBinding? lifecycleExecutionStart =
            dispatchRequest.RequiredStart;
        var lifecycleActionDispatched = false;
        var lifecycleCompletionDeadlineStarted = false;
        var requiresFixedHostProof = initialSessionAcquisition is not null;
        UnityRequestExecutionResult RetainLifecycleStart (UnityRequestExecutionResult result)
        {
            return result.WithLifecycleExecutionStart(
                lifecycleExecutionStart,
                lifecycleActionDispatched);
        }

        var responseReplayPolicy = dispatchRequest.ResponseReplayPolicy;
        if (lifecycleExecutionStart is not null)
        {
            dispatchObservation?.ReportStarted(lifecycleExecutionStart);
        }
        var acquisitionScope = initialAcquisitionScope
            ?? sessionAcquisitionCoordinator.CreateScope(deadline);
        var sessionAcquisition = initialSessionAcquisition
            ?? await acquisitionScope.ResolveCurrentAsync(
                    unityProject,
                    dispatchCancellationToken)
                .ConfigureAwait(false);

        while (true)
        {
            if (dispatchRequest.RequiredStart is not null
                && ProcessLivenessProbe.ObserveIdentity(
                    dispatchRequest.RequiredStart.Host.Process)
                == ProcessIdentityObservation.ConfirmedExitedOrReplaced)
            {
                return CreateConfirmedHostExitResult(
                    dispatchRequest.RequiredStart,
                    lifecycleActionDispatched);
            }

            switch (sessionAcquisition.Kind)
            {
                case DaemonSessionAcquisitionKind.Success:
                    break;
                case DaemonSessionAcquisitionKind.RequestDeadlineExpired:
                    return RetainLifecycleStart(
                        firstResponseInterruption is null
                            ? CreateDeadlineExceededResult(deadline.Timeout)
                            : CreateInterruptedResponseTimeoutResult(
                                firstResponseInterruption,
                                deadline.Timeout));
                case DaemonSessionAcquisitionKind.PublicationWindowExpired:
                    if (firstResponseInterruption is not null)
                    {
                        return RetainLifecycleStart(
                            CreateInterruptedResponseUnavailableResult(
                                firstResponseInterruption,
                                sessionAcquisition));
                    }
                    if (lifecycleExecutionStart is not null)
                    {
                        return RetainLifecycleStart(
                            CreateReconnectEndpointUnavailableResult());
                    }

                    return RetainLifecycleStart(
                        sessionTokenRejection is not null
                            ? UnityRequestExecutionResult.Success(
                                UnityRequestResponseFactory.Create(sessionTokenRejection))
                            : UnityRequestExecutionResult.Failure(
                                UnityIpcFailureClassifier.FromCodeAndMessage(
                                    UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                                    DaemonSessionAcquisitionResult.SessionNotAvailableMessage)));
                case DaemonSessionAcquisitionKind.SessionNotAvailable:
                    if (firstResponseInterruption is not null || sessionTokenRejection is not null)
                    {
                        throw new InvalidOperationException(
                            "Session-not-available is valid only during initial daemon session resolution.");
                    }

                    return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.FromCodeAndMessage(
                        UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                        DaemonSessionAcquisitionResult.SessionNotAvailableMessage));
                case DaemonSessionAcquisitionKind.EndpointAvailabilityWindowExpired:
                    if (firstResponseInterruption is not null)
                    {
                        return RetainLifecycleStart(
                            CreateInterruptedResponseUnavailableResult(
                                firstResponseInterruption,
                                sessionAcquisition));
                    }
                    if (lifecycleExecutionStart is not null)
                    {
                        return RetainLifecycleStart(
                            CreateReconnectEndpointUnavailableResult());
                    }
                    return RetainLifecycleStart(
                        UnityRequestExecutionResult.Failure(
                            UnityIpcFailureClassifier.FromCodeAndMessage(
                                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                                DaemonSessionAcquisitionResult.SessionNotAvailableMessage)));
                case DaemonSessionAcquisitionKind.HostIdentityMismatch:
                    if (firstResponseInterruption is not null)
                    {
                        return RetainLifecycleStart(
                            CreateInterruptedResponseUnavailableResult(
                                firstResponseInterruption,
                                sessionAcquisition));
                    }
                    if (lifecycleExecutionStart is not null)
                    {
                        return RetainLifecycleStart(
                            CreateReconnectHostMismatchResult());
                    }

                    throw new InvalidOperationException(
                        "A durable host mismatch requires a confirmed Lifecycle Execution start or response interruption.");
                case DaemonSessionAcquisitionKind.SessionReadFailure:
                    if (firstResponseInterruption is not null)
                    {
                        return RetainLifecycleStart(
                            CreateInterruptedResponseUnavailableResult(
                                firstResponseInterruption,
                                sessionAcquisition));
                    }

                    return RetainLifecycleStart(
                        UnityRequestExecutionResult.Failure(
                            UnityIpcFailureClassifier.InternalError(
                                $"Daemon session could not be read. {sessionAcquisition.ReadFailure!.Error!.Message}")));
                default:
                    throw new InvalidOperationException(
                        $"Unsupported daemon session acquisition outcome: {sessionAcquisition.Kind}.");
            }

            dispatchCancellationToken.ThrowIfCancellationRequested();
            if (!TryGetDispatchBudget(
                    deadline,
                    out var remainingTimeout,
                    out var remainingMilliseconds,
                    out var requestDeadlineUtc))
            {
                return RetainLifecycleStart(
                    firstResponseInterruption is null
                        ? CreateDeadlineExceededResult(deadline.Timeout)
                        : CreateInterruptedResponseTimeoutResult(
                            firstResponseInterruption,
                            deadline.Timeout));
            }

            var session = sessionAcquisition.Session!;
            if (dispatchRequest.RequiredStart is not null
                && !MatchesRequiredStartHost(
                    session,
                    dispatchRequest.RequiredStart))
            {
                return RetainLifecycleStart(CreateReconnectHostMismatchResult());
            }
            if (lifecycleExecutionStart is not null
                && !acquisitionScope.TryBindDurableHost(session))
            {
                return RetainLifecycleStart(CreateReconnectHostMismatchResult());
            }

            try
            {
                var endpoint = DaemonSessionIpcTransportEndpointAdapter.Adapt(session);
                IpcResponse? response = null;
                var actionPayload = dispatchRequest.Registration == null
                    ? dispatchRequest.Payload
                    : default;
                if (dispatchRequest.Registration != null)
                {
                    if (dispatchRequest.BeginsLifecycleExecution
                        && !lifecycleCompletionDeadlineStarted)
                    {
                        // A first Start write may have persisted even when its response is lost.
                        // From this ambiguity boundary onward, retain only the response-delivery
                        // grace; the Unity handler still rejects a new Start after the immutable
                        // execution deadline carried by its registration.
                        deadline = deadline.CreateCompletionDeadline(
                            LifecycleExecutionTiming.ResponseDeliveryGrace);
                        acquisitionScope =
                            sessionAcquisitionCoordinator.CreateScope(deadline);
                        if (lifecycleExecutionStart is not null
                            && !acquisitionScope.TryBindDurableHost(session))
                        {
                            return RetainLifecycleStart(
                                CreateReconnectHostMismatchResult());
                        }
                        lifecycleCompletionDeadlineStarted = true;
                        if (!TryGetDispatchBudget(
                                deadline,
                                out remainingTimeout,
                                out remainingMilliseconds,
                                out requestDeadlineUtc))
                        {
                            return RetainLifecycleStart(
                                CreateDeadlineExceededResult(deadline.Timeout));
                        }
                    }

                    // From the first Lifecycle Execution start write onward, the durable execution
                    // owns delivery and replay. Caller cancellation only stops waiting for its result.
                    dispatchCancellationToken = CancellationToken.None;
                    if (lifecycleExecutionStart is not null)
                    {
                        // The observer already accepted this immutable Start
                        // Record. A response replay may use a permitted
                        // endpoint successor, but it must never issue another
                        // Lifecycle Start exchange or observe a new binding.
                        actionPayload = dispatchRequest.CreateLifecycleActionPayload(
                            lifecycleExecutionStart);
                    }
                    else
                    {
                        var lifecycleStartResponse = await transportClient.SendAsync(
                                endpoint,
                                LifecycleExecutionStartExchange.CreateRequest(
                                    dispatchRequest,
                                    session.SessionToken,
                                    lifecycleStartRequestId,
                                    requestDeadlineUtc,
                                    remainingMilliseconds),
                                remainingTimeout,
                                dispatchCancellationToken)
                            .ConfigureAwait(false);
                        if (IsSessionTokenInvalid(lifecycleStartResponse))
                        {
                            response = lifecycleStartResponse;
                        }
                        else
                        {
                            switch (LifecycleExecutionStartExchange
                                .InterpretResponse(
                                    dispatchRequest,
                                    lifecycleStartResponse))
                            {
                                case LifecycleExecutionStartExchange
                                    .ProviderRejected rejected:
                                    return RetainLifecycleStart(
                                        UnityRequestExecutionResult.Success(
                                            UnityRequestResponseFactory.Create(
                                                rejected.Response)));
                                case LifecycleExecutionStartExchange.Invalid invalid:
                                    return RetainLifecycleStart(
                                        UnityRequestExecutionResult.Failure(
                                            invalid.Failure));
                                case LifecycleExecutionStartExchange
                                    .Mismatched mismatched:
                                    return RetainLifecycleStart(
                                        CreateReconnectStartMismatchResult(
                                            mismatched.Code));
                                case LifecycleExecutionStartExchange.Confirmed confirmed:
                                    if ((requiresFixedHostProof
                                            && !MatchesRequiredStartHost(
                                                session,
                                                confirmed.Start))
                                        || !acquisitionScope.TryBindDurableHost(session))
                                    {
                                        return RetainLifecycleStart(
                                            CreateReconnectHostMismatchResult());
                                    }
                                    actionPayload = confirmed.ActionPayload;
                                    lifecycleExecutionStart = confirmed.Start;
                                    var startObservation = await ObserveStartAsync(
                                            dispatchRequest,
                                            confirmed.Start,
                                            deadline,
                                            dispatchObservation)
                                        .ConfigureAwait(false);
                                    if (startObservation is not null)
                                    {
                                        return RetainLifecycleStart(startObservation);
                                    }
                                    break;
                                default:
                                    throw new InvalidOperationException(
                                        "Unsupported Lifecycle Execution start interpretation.");
                            }
                        }
                    }
                }

                if (response == null)
                {
                    if (!TryGetDispatchBudget(
                            deadline,
                            out remainingTimeout,
                            out remainingMilliseconds,
                            out requestDeadlineUtc))
                    {
                        return RetainLifecycleStart(
                            firstResponseInterruption is null
                                ? CreateDeadlineExceededResult(deadline.Timeout)
                                : CreateInterruptedResponseTimeoutResult(
                                    firstResponseInterruption,
                                    deadline.Timeout));
                    }

                    if (lifecycleExecutionStart != null)
                    {
                        lifecycleActionDispatched = true;
                        dispatchObservation?.ReportActionDispatched();
                    }

                    var responseAttempt = sendAttempt(
                        endpoint,
                        UnityIpcRequestFactory.Create(
                            session.SessionToken,
                            dispatchRequest.Method,
                            actionPayload,
                            requestId,
                            responseMode,
                            requestDeadlineUtc,
                            remainingMilliseconds),
                        remainingTimeout,
                        dispatchCancellationToken);
                    response = await responseAttempt.ConfigureAwait(false);
                }

                if (IsSessionTokenInvalid(response))
                {
                    sessionTokenRejection = response;
                    if (lifecycleExecutionStart is not null
                        && responseReplayPolicy == UnityIpcResponseReplayPolicy.LifecycleExecutionSameHostSuccessor)
                    {
                        sessionAcquisition = await acquisitionScope.ResolveDurableReplacementAsync(
                                unityProject,
                                session,
                                dispatchCancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        firstResponseInterruption = null;
                        sessionAcquisition = await acquisitionScope.ResolveReplacementAsync(
                                unityProject,
                                session,
                                dispatchCancellationToken)
                            .ConfigureAwait(false);
                    }

                    continue;
                }

                return RetainLifecycleStart(
                    UnityRequestExecutionResult.Success(
                        UnityRequestResponseFactory.Create(response)));
            }
            catch (OperationCanceledException) when (dispatchCancellationToken.IsCancellationRequested)
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
                if (lifecycleExecutionStart is null
                    && dispatchRequest.Registration is not null
                    && IsRecoverableResponseInterruption(exception))
                {
                    lifecycleExecutionStart =
                        await LifecycleExecutionStartRecordRecovery.TryReadAsync(
                                unityProject,
                                dispatchRequest)
                            .ConfigureAwait(false);
                    if (lifecycleExecutionStart is not null)
                    {
                        if ((requiresFixedHostProof
                                && !MatchesRequiredStartHost(
                                    session,
                                    lifecycleExecutionStart))
                            || !acquisitionScope.TryBindDurableHost(session))
                        {
                            return RetainLifecycleStart(
                                CreateReconnectHostMismatchResult());
                        }
                        var startObservation = await ObserveStartAsync(
                                dispatchRequest,
                                lifecycleExecutionStart,
                                deadline,
                                dispatchObservation)
                            .ConfigureAwait(false);
                        if (startObservation is not null)
                        {
                            return RetainLifecycleStart(startObservation);
                        }
                    }
                }

                if (!isRetryableBeforeRequestWrite
                    && responseReplayPolicy == UnityIpcResponseReplayPolicy.None
                    && IsRecoverableResponseInterruption(exception))
                {
                    return CreateNonReplayableResponseInterruptionResult(
                        exception,
                        deadline.Timeout);
                }

                if (sessionTokenRejection is not null
                    && firstResponseInterruption is null
                    && !isRetryableBeforeRequestWrite
                    && (responseReplayPolicy == UnityIpcResponseReplayPolicy.None
                        || !IsRecoverableResponseInterruption(exception)))
                {
                    return RetainLifecycleStart(
                        UnityRequestExecutionResult.Failure(
                            UnityIpcFailureClassifier.FromDaemonDispatchException(
                                exception,
                                remainingTimeout)));
                }

                if (isRetryableBeforeRequestWrite)
                {
                    if (lifecycleExecutionStart is not null
                        && responseReplayPolicy == UnityIpcResponseReplayPolicy.LifecycleExecutionSameHostSuccessor)
                    {
                        sessionAcquisition = await acquisitionScope.ResolveAfterDurablePreWriteFailureAsync(
                                unityProject,
                                session,
                                dispatchCancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        firstResponseInterruption = null;
                        sessionAcquisition = await acquisitionScope.ResolveAfterPreWriteFailureAsync(
                                unityProject,
                                session,
                                dispatchCancellationToken)
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
                                        dispatchCancellationToken)
                                    .ConfigureAwait(false),
                            UnityIpcResponseReplayPolicy.LifecycleExecutionSameHostSuccessor =>
                                await acquisitionScope.ResolveAfterDurableResponseInterruptionAsync(
                                        unityProject,
                                        session,
                                        dispatchCancellationToken)
                                    .ConfigureAwait(false),
                            _ => throw new InvalidOperationException(
                                $"Unsupported IPC response replay policy: {responseReplayPolicy}."),
                        };
                        continue;
                    }
                }

                if (firstResponseInterruption is not null)
                {
                    return RetainLifecycleStart(
                        deadline.IsExpired
                            ? CreateInterruptedResponseTimeoutResult(
                                firstResponseInterruption,
                                deadline.Timeout)
                            : CreateInterruptedResponseReplayFailureResult(
                                firstResponseInterruption,
                                exception));
                }

                if (deadline.IsExpired)
                {
                    return RetainLifecycleStart(
                        CreateDeadlineExceededResult(deadline.Timeout));
                }

                return RetainLifecycleStart(
                    UnityRequestExecutionResult.Failure(
                        UnityIpcFailureClassifier.FromDaemonDispatchException(
                            exception,
                            remainingTimeout)));
            }
        }
    }

    private static bool MatchesRequiredStartHost (
        DaemonSession session,
        LifecycleExecutionStartBinding requiredStart)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(requiredStart);
        if (session.ProjectFingerprint
                != requiredStart.Project.ProjectFingerprint
            || session.ProcessId
                != requiredStart.Host.Process.ProcessId)
        {
            return false;
        }

        return session.EditorMode switch
        {
            UnityEditorMode.Batchmode => true,
            UnityEditorMode.Gui =>
                session.EditorInstanceId
                    == requiredStart.Host.EditorInstanceId,
            _ => false,
        };
    }

    private static UnityRequestExecutionResult
        CreateReconnectHostMismatchResult ()
    {
        return UnityRequestExecutionResult.Failure(
            UnityIpcFailureClassifier.FromCodeAndMessage(
                LifecycleExecutionErrorCodes.HostMismatch,
                "The daemon session does not belong to the Unity Editor host fixed by the Lifecycle Execution start."));
    }

    private static async ValueTask<UnityRequestExecutionResult?> ObserveStartAsync (
        UnityIpcDispatchRequest dispatchRequest,
        LifecycleExecutionStartBinding start,
        ExecutionDeadline deadline,
        LifecycleExecutionDispatchObservation? dispatchObservation)
    {
        var observation = await dispatchRequest
            .ObserveLifecycleStartAsync(start)
            .ConfigureAwait(false);
        if (observation is LifecycleExecutionStartObservation.Rejected rejected)
        {
            return UnityRequestExecutionResult.Failure(
                UnityIpcFailureClassifier.FromCodeAndMessage(
                    rejected.Failure.Code,
                    rejected.Failure.Message),
                start);
        }

        dispatchObservation?.ReportStarted(start);
        return null;
    }

    private static UnityRequestExecutionResult CreateConfirmedHostExitResult (
        LifecycleExecutionStartBinding requiredStart,
        bool lifecycleActionDispatched)
    {
        return UnityRequestExecutionResult.Failure(
            UnityIpcFailureClassifier.FromCodeAndMessage(
                EditorLifecycleErrorCodes.EditorUnavailable,
                "The Unity Editor process that owns the Lifecycle Execution is no longer running."),
            requiredStart,
            lifecycleActionDispatched,
            new LifecycleExecutionHostExitObservation(
                requiredStart.Host.Process));
    }

    private static UnityRequestExecutionResult
        CreateReconnectEndpointUnavailableResult ()
    {
        return UnityRequestExecutionResult.Failure(
            UnityIpcFailureClassifier.FromCodeAndMessage(
                EditorLifecycleErrorCodes.EditorUnavailable,
                "The Unity Editor host fixed by the Lifecycle Execution start did not publish a reachable successor endpoint."));
    }

    private static UnityRequestExecutionResult
        CreateReconnectStartMismatchResult (UcliCode mismatchCode)
    {
        ArgumentNullException.ThrowIfNull(mismatchCode);
        return UnityRequestExecutionResult.Failure(
            UnityIpcFailureClassifier.FromCodeAndMessage(
                mismatchCode,
                "The daemon returned a Lifecycle Execution start that does not match the authoritative persisted start."));
    }

    private static bool TryGetDispatchBudget (
        ExecutionDeadline deadline,
        out TimeSpan remainingTimeout,
        out int remainingMilliseconds,
        out DateTimeOffset requestDeadlineUtc)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        requestDeadlineUtc = deadline.UtcDeadline;
        if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
        {
            remainingMilliseconds = 0;
            return false;
        }

        var utcRemaining = requestDeadlineUtc - deadline.Clock.GetUtcNow();
        if (utcRemaining <= TimeSpan.Zero)
        {
            remainingTimeout = TimeSpan.Zero;
            remainingMilliseconds = 0;
            return false;
        }

        if (utcRemaining < remainingTimeout)
        {
            remainingTimeout = utcRemaining;
        }

        var roundedMilliseconds = Math.Ceiling(remainingTimeout.TotalMilliseconds);
        remainingMilliseconds = roundedMilliseconds >= int.MaxValue
            ? int.MaxValue
            : (int)roundedMilliseconds;
        return remainingMilliseconds > 0;
    }

    private static UnityRequestExecutionResult CreateDeadlineExceededResult (TimeSpan timeout)
    {
        return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.Timeout(
            $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
    }

    private static UnityRequestExecutionResult CreateInterruptedResponseTimeoutResult (
        Exception interruption,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(interruption);
        return UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            UnityRequestFailureKind.TransportInterrupted,
            ExecutionErrorCodes.IpcTimeout,
            $"Unity daemon IPC response was interrupted and the request deadline expired after "
            + $"{timeout.TotalMilliseconds:0} milliseconds. {interruption.Message}"));
    }

    private static UnityRequestExecutionResult CreateInterruptedResponseUnavailableResult (
        Exception interruption,
        DaemonSessionAcquisitionResult acquisition)
    {
        ArgumentNullException.ThrowIfNull(acquisition);
        var recoveryFailure = acquisition.Kind switch
        {
            DaemonSessionAcquisitionKind.PublicationWindowExpired =>
                "No successor daemon session was published within the recovery window.",
            DaemonSessionAcquisitionKind.EndpointAvailabilityWindowExpired =>
                "The successor daemon endpoint did not become available within the recovery window.",
            DaemonSessionAcquisitionKind.HostIdentityMismatch =>
                "The published successor session belongs to a different Unity Editor host.",
            DaemonSessionAcquisitionKind.SessionReadFailure =>
                $"Daemon session metadata could not be read. {acquisition.ReadFailure!.Error!.Message}",
            _ => throw new ArgumentOutOfRangeException(
                nameof(acquisition),
                acquisition.Kind,
                "The acquisition outcome does not describe an unavailable interrupted response."),
        };

        ArgumentNullException.ThrowIfNull(interruption);
        return UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            UnityRequestFailureKind.TransportInterrupted,
            EditorLifecycleErrorCodes.EditorUnavailable,
            $"Unity daemon IPC response was interrupted and could not be recovered. "
            + $"{recoveryFailure} Original interruption: {interruption.Message}"));
    }

    private static UnityRequestExecutionResult CreateInterruptedResponseReplayFailureResult (
        Exception interruption,
        Exception replayFailure)
    {
        ArgumentNullException.ThrowIfNull(interruption);
        ArgumentNullException.ThrowIfNull(replayFailure);
        return UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            UnityRequestFailureKind.TransportInterrupted,
            UcliCoreErrorCodes.InternalError,
            $"Unity daemon IPC response was interrupted and could not be recovered. "
            + $"The replay attempt failed: {replayFailure.Message} "
            + $"Original interruption: {interruption.Message}"));
    }

    private static UnityRequestExecutionResult CreateNonReplayableResponseInterruptionResult (
        Exception interruption,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(interruption);
        if (interruption is TimeoutException)
        {
            return CreateInterruptedResponseTimeoutResult(interruption, timeout);
        }

        return UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            UnityRequestFailureKind.TransportInterrupted,
            UcliCoreErrorCodes.InternalError,
            $"Unity daemon IPC response was interrupted after the request was sent. "
            + $"The request cannot be replayed safely. {interruption.Message}"));
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

/// <summary> Contains the daemon session fixed for one Lifecycle Execution host binding. </summary>
internal sealed record DaemonHostBindingResult (
    DaemonSession? Session,
    UnityRequestFailure? Failure)
{
    public bool IsSuccess => Session is not null;

    public static DaemonHostBindingResult Success (DaemonSession session) => new(
        session ?? throw new ArgumentNullException(nameof(session)),
        Failure: null);

    public static DaemonHostBindingResult Rejected (UnityRequestFailure failure) => new(
        Session: null,
        failure ?? throw new ArgumentNullException(nameof(failure)));
}
