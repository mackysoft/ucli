using MackySoft.Ucli.Application.Features.Recording.Artifacts;
using MackySoft.Ucli.Application.Features.Recording.Capability;
using MackySoft.Ucli.Application.Features.Recording.Finalization;
using MackySoft.Ucli.Application.Features.Recording.Projection;
using MackySoft.Ucli.Application.Features.Recording.Registry;
using MackySoft.Ucli.Application.Features.Recording.Requests;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Recording;
using GameViewRecordingStartServiceResult = MackySoft.Ucli.Application.Features.Recording.UseCases.GameViewRecordingServiceResult<MackySoft.Ucli.Contracts.Recording.GameViewRecordingExecutionPayload>;
using GameViewRecordingStatusServiceResult = MackySoft.Ucli.Application.Features.Recording.UseCases.GameViewRecordingServiceResult<MackySoft.Ucli.Contracts.Recording.GameViewRecordingStatusPayload>;
using GameViewRecordingStopServiceResult = MackySoft.Ucli.Application.Features.Recording.UseCases.GameViewRecordingServiceResult<MackySoft.Ucli.Contracts.Recording.GameViewRecordingStopResultPayload>;

namespace MackySoft.Ucli.Application.Features.Recording.UseCases;

/// <summary>Owns recording admission, durable correlation, runtime observation, and terminal publication.</summary>
internal sealed class GameViewRecordingService : IGameViewRecordingService
{
    private static readonly TimeSpan MonitoringPollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RuntimeStatusRequestLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartDispatchRequestLimit = TimeSpan.FromSeconds(5);

    private readonly IProjectContextResolver projectContextResolver;
    private readonly GameViewRecordingCapabilityResolver capabilityResolver;
    private readonly IUnityRequestExecutor unityRequestExecutor;
    private readonly IGameViewRecordingArtifactStore artifactStore;
    private readonly IGameViewRecordingExecutionStore executionStore;
    private readonly IGameViewRecordingTerminalFinalizer terminalFinalizer;
    private readonly IProcessIdentityObserver processIdentityObserver;
    private readonly IGuidGenerator recordingIdGenerator;
    private readonly TimeProvider timeProvider;

    public GameViewRecordingService (
        IProjectContextResolver projectContextResolver,
        GameViewRecordingCapabilityResolver capabilityResolver,
        IUnityRequestExecutor unityRequestExecutor,
        IGameViewRecordingArtifactStore artifactStore,
        IGameViewRecordingExecutionStore executionStore,
        IGameViewRecordingTerminalFinalizer terminalFinalizer,
        IProcessIdentityObserver processIdentityObserver,
        IGuidGenerator recordingIdGenerator,
        TimeProvider timeProvider)
    {
        this.projectContextResolver = projectContextResolver
            ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.capabilityResolver = capabilityResolver
            ?? throw new ArgumentNullException(nameof(capabilityResolver));
        this.unityRequestExecutor = unityRequestExecutor
            ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.artifactStore = artifactStore
            ?? throw new ArgumentNullException(nameof(artifactStore));
        this.executionStore = executionStore
            ?? throw new ArgumentNullException(nameof(executionStore));
        this.terminalFinalizer = terminalFinalizer
            ?? throw new ArgumentNullException(nameof(terminalFinalizer));
        this.processIdentityObserver = processIdentityObserver
            ?? throw new ArgumentNullException(nameof(processIdentityObserver));
        this.recordingIdGenerator = recordingIdGenerator
            ?? throw new ArgumentNullException(nameof(recordingIdGenerator));
        this.timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async ValueTask<GameViewRecordingStartServiceResult> StartAsync (
        GameViewRecordingStartInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var deadlineObservedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        var deadlineObservedTimestamp = timeProvider.GetTimestamp();
        cancellationToken.ThrowIfCancellationRequested();
        if (input.RecordingId == Guid.Empty)
        {
            return Invalid("recordingId must not be the empty UUID.");
        }
        if (string.IsNullOrWhiteSpace(input.RequestJson))
        {
            return Invalid("Recording request JSON must not be empty.");
        }

        var parsed = GameViewRecordingRequestParser.Parse(input.RequestJson);
        if (!parsed.IsSuccess)
        {
            return GameViewRecordingStartServiceResult.Failure(parsed.Error!);
        }

        var contextResult = await projectContextResolver
            .ResolveAsync(input.ProjectPath, cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return GameViewRecordingStartServiceResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.RecordingStart,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return GameViewRecordingStartServiceResult.Failure(timeoutResult.Error!);
        }

        var deadline = ExecutionDeadline.StartFromObservation(
            timeoutResult.Timeout!.Value,
            deadlineObservedAtUtc,
            deadlineObservedTimestamp,
            timeProvider);
        if (input.RecordingId.HasValue)
        {
            GameViewRecordingStoredExecution? existing;
            try
            {
                existing = await executionStore.ReadAsync(
                        context.UnityProject,
                        input.RecordingId.Value,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                var latest = await executionStore.ReadAsync(
                        context.UnityProject,
                        input.RecordingId.Value,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                return CallerWaitCanceled(latest?.Payload);
            }

            if (existing is not null)
            {
                return await ResolveRepeatedStartAsync(
                        context,
                        parsed.Request!,
                        existing,
                        input.Detach,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (!deadline.TryGetRemainingTimeout(out var capabilityTimeout))
        {
            return StartAdmissionTimeout();
        }

        GameViewRecordingCapabilityResolution capabilityResolution;
        try
        {
            capabilityResolution = await capabilityResolver.ResolveAsync(
                    context,
                    UcliCommandIds.RecordingStart,
                    capabilityTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var latest = input.RecordingId.HasValue
                ? await executionStore.ReadAsync(
                        context.UnityProject,
                        input.RecordingId.Value,
                        CancellationToken.None)
                    .ConfigureAwait(false)
                : null;
            return CallerWaitCanceled(latest?.Payload);
        }
        var capability = capabilityResolution.Capability;
        ReadyGameViewRecordingAdmission admission;
        if (capabilityResolution is RejectedGameViewRecordingAdmission rejection)
        {
            if (input.RecordingId is Guid explicitRecordingId)
            {
                GameViewRecordingStoredExecution? existing;
                try
                {
                    existing = await executionStore.ReadAsync(
                            context.UnityProject,
                            explicitRecordingId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    var latest = await executionStore.ReadAsync(
                            context.UnityProject,
                            explicitRecordingId,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    return CallerWaitCanceled(latest?.Payload);
                }

                if (existing is not null)
                {
                    return await ResolveRepeatedStartAsync(
                            context,
                            parsed.Request!,
                            existing,
                            input.Detach,
                            deadline,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            return GameViewRecordingStartServiceResult.Failure(rejection.Error);
        }

        admission = capabilityResolution as ReadyGameViewRecordingAdmission
            ?? throw new InvalidOperationException("Recording capability resolution kind is not defined.");

        var effectiveResult = Normalize(parsed.Request!, admission.Limits);
        if (!effectiveResult.IsSuccess)
        {
            return GameViewRecordingStartServiceResult.Failure(effectiveResult.Error!);
        }

        var effective = effectiveResult.Request!;
        var recordingId = input.RecordingId ?? recordingIdGenerator.Generate();
        if (!deadline.TryGetRemainingTimeout(out var admissionTimeout))
        {
            return StartAdmissionTimeout();
        }

        IGameViewRecordingAdmissionLease? admissionLease;
        try
        {
            admissionLease = await executionStore.TryAcquireAdmissionLeaseAsync(
                    context.UnityProject,
                    recordingId,
                    admission.StartBinding,
                    admissionTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var latest = await executionStore.ReadAsync(
                    context.UnityProject,
                    recordingId,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return CallerWaitCanceled(latest?.Payload);
        }

        if (admissionLease is null)
        {
            var sameId = await executionStore.ReadAsync(
                    context.UnityProject,
                    recordingId,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (sameId is not null)
            {
                return await ResolveRepeatedStartAsync(
                        context,
                        parsed.Request!,
                        sameId,
                        input.Detach,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var current = await executionStore.ReadCurrentAsync(
                    context.UnityProject,
                    admission.StartBinding.Runtime.RuntimeId,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return current is null
                ? StartAdmissionTimeout()
                : Conflict(current.RecordingId);
        }

        GameViewRecordingStoredExecution? admittedExisting = null;
        var admissionTransferred = false;
        try
        {
            admittedExisting = await executionStore.ReadAsync(
                    context.UnityProject,
                    recordingId,
                    cancellationToken)
                .ConfigureAwait(false);
            admittedExisting ??= await executionStore.ReadCurrentAsync(
                    context.UnityProject,
                    admission.StartBinding.Runtime.RuntimeId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (admittedExisting is null)
            {
                if (!deadline.TryGetRemainingTimeout(out _))
                {
                    return StartAdmissionTimeout();
                }

                var preparation = artifactStore.Prepare(
                    context.UnityProject,
                    recordingId,
                    admissionLease);
                if (!preparation.IsSuccess)
                {
                    return GameViewRecordingStartServiceResult.Failure(preparation.Error!);
                }

                var lease = preparation.Lease!;
                var requestPublication = await lease.PublishRequestAsync(
                        effective,
                        knownArtifact: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!requestPublication.IsSuccess)
                {
                    return GameViewRecordingStartServiceResult.Failure(requestPublication.Error!);
                }

                var requestRef = requestPublication.Artifact!;
                if (!deadline.TryGetRemainingTimeout(out var startDispatchBudget))
                {
                    await lease.DiscardUnregisteredArtifactsAsync(requestRef, CancellationToken.None)
                        .ConfigureAwait(false);
                    return StartAdmissionTimeout();
                }

                var startDispatchDeadlineUtc = timeProvider.GetUtcNow().ToUniversalTime()
                    + CapStartDispatchTimeout(startDispatchBudget);
                var preparingPayload = GameViewRecordingPayloadFactory.CreatePreparing(
                    context.UnityProject,
                    recordingId,
                    effective,
                    requestRef,
                    timeProvider.GetUtcNow());
                var stored = CreateStored(
                    recordingId,
                    effective,
                    requestRef,
                    capability,
                    admission.StartBinding,
                    startDispatchDeadlineUtc,
                    runtimeSnapshot: null,
                    preparingPayload);
                if (cancellationToken.IsCancellationRequested)
                {
                    await lease.DiscardUnregisteredArtifactsAsync(requestRef, CancellationToken.None)
                        .ConfigureAwait(false);
                    return CallerWaitCanceled(payload: null);
                }

                var registration = await admissionLease.TryRegisterAsync(
                        lease.ExecutionStatePath,
                        stored,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (!registration.Registered)
                {
                    var discard = await lease.DiscardUnregisteredArtifactsAsync(
                            requestRef,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    if (!discard.IsSuccess)
                    {
                        return GameViewRecordingStartServiceResult.Failure(discard.Error!);
                    }

                    admittedExisting = registration.Existing!;
                }
                else
                {
                    admissionTransferred = true;
                    return await StartRegisteredAsync(
                            context,
                            lease,
                            stored,
                            input.Detach,
                            mayHavePriorDispatch: false,
                            deadline,
                            admissionLease,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var latest = await executionStore.ReadAsync(
                    context.UnityProject,
                    recordingId,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return CallerWaitCanceled(latest?.Payload);
        }
        finally
        {
            if (!admissionTransferred)
            {
                admissionLease.Dispose();
            }
        }

        return admittedExisting!.RecordingId == recordingId
            ? await ResolveRepeatedStartAsync(
                    context,
                    parsed.Request!,
                    admittedExisting,
                    input.Detach,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false)
            : Conflict(admittedExisting.RecordingId);
    }

    private async ValueTask<GameViewRecordingStartServiceResult> StartRegisteredAsync (
        ProjectContext context,
        IGameViewRecordingArtifactLease lease,
        GameViewRecordingStoredExecution stored,
        bool detach,
        bool mayHavePriorDispatch,
        ExecutionDeadline deadline,
        IGameViewRecordingAdmissionLease? admissionLease,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetStartDispatchTimeout(stored, out var startTimeout))
            {
                if (!mayHavePriorDispatch)
                {
                    // No Unity start request was issued for this initial registration, so expiry proves no start was dispatched.
                    var finalized = await FinalizeDispatchDeadlineExceededAsync(
                            context,
                            lease,
                            stored,
                            StartDispatchRequestLimit,
                            deadline,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return finalized is RecordingRefreshFailure finalizationFailure
                        ? GameViewRecordingStartServiceResult.Failure(
                            finalizationFailure.Error,
                            finalizationFailure.Stored.Payload)
                        : !finalized.Stored.Payload.IsTerminal
                            && !deadline.TryGetRemainingTimeout(out _)
                            ? MonitoringTimeout(finalized.Stored.Payload)
                        : GameViewRecordingStartServiceResult.Success(finalized.Stored.Payload);
                }

                admissionLease?.Dispose();
                admissionLease = null;
                return await ObserveExistingStartAsync(
                        context,
                        stored,
                        detach,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (admissionLease is null)
            {
                if (!deadline.TryGetRemainingTimeout(out var admissionTimeout))
                {
                    return MonitoringTimeout(stored.Payload);
                }

                admissionLease = await executionStore.TryAcquireAdmissionLeaseAsync(
                        context.UnityProject,
                        stored.RecordingId,
                        stored.StartBinding,
                        admissionTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (admissionLease is null)
                {
                    var latest = await executionStore.ReadAsync(
                            context.UnityProject,
                            stored.RecordingId,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    return MonitoringTimeout((latest ?? stored).Payload);
                }
            }

            StartDispatchContinuation continuation;
            try
            {
                var durable = await executionStore.ReadAsync(
                        context.UnityProject,
                        stored.RecordingId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (durable is null)
                {
                    return NotFound(stored.RecordingId);
                }
                stored = durable;
                if (stored.Payload.IsTerminal)
                {
                    return GameViewRecordingStartServiceResult.Success(stored.Payload);
                }

                if (!deadline.TryGetRemainingTimeout(out var remaining))
                {
                    return MonitoringTimeout(stored.Payload);
                }

                if (!TryGetStartDispatchTimeout(stored, out var dispatchRemaining))
                {
                    if (mayHavePriorDispatch)
                    {
                        return await ObserveExistingStartAsync(
                                context,
                                stored,
                                detach,
                                deadline,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    var finalized = await FinalizeDispatchDeadlineExceededAsync(
                            context,
                            lease,
                            stored,
                            StartDispatchRequestLimit,
                            deadline,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return finalized is RecordingRefreshFailure finalizationFailure
                        ? GameViewRecordingStartServiceResult.Failure(
                            finalizationFailure.Error,
                            finalizationFailure.Stored.Payload)
                        : GameViewRecordingStartServiceResult.Success(finalized.Stored.Payload);
                }

                startTimeout = CapStartDispatchTimeout(
                    remaining < dispatchRemaining ? remaining : dispatchRemaining);

                var startRequest = new IpcGameViewRecordingStartRequest(
                    stored.RecordingId,
                    stored.RequestDigest,
                    stored.Request,
                    stored.StartBinding,
                    stored.StartDispatchDeadlineUtc);
                var startExecution = await unityRequestExecutor.ExecuteAsync(
                        UcliCommandIds.RecordingStart,
                        UnityExecutionMode.Daemon,
                        startTimeout,
                        context.Config,
                        context.UnityProject,
                        new UnityRequestPayload.RecordingStart(startRequest),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!startExecution.IsSuccess)
                {
                    var executionError = CreateRuntimeError(startExecution.FailureInfo!);
                    if (executionError.Kind == ExecutionErrorKind.Canceled)
                    {
                        return await CallerWaitCanceledAsync(context, stored).ConfigureAwait(false);
                    }

                    return GameViewRecordingStartServiceResult.Failure(
                        executionError,
                        (await ReadLatestAsync(context, stored).ConfigureAwait(false)).Payload);
                }

                if (startExecution.Response!.Errors.Count != 0)
                {
                    var failure = CreateRuntimeError(startExecution.Response.Errors[0]);
                    if (failure.Kind == ExecutionErrorKind.Canceled)
                    {
                        return await CallerWaitCanceledAsync(context, stored).ConfigureAwait(false);
                    }
                    if (startExecution.Response.Errors[0].Code
                        == GameViewRecordingErrorCodes.DispatchDeadlineExceeded
                        && !mayHavePriorDispatch)
                    {
                        var finalized = await FinalizeDispatchDeadlineExceededAsync(
                                context,
                                lease,
                                stored,
                                startTimeout,
                                deadline,
                                cancellationToken)
                            .ConfigureAwait(false);
                        return finalized is RecordingRefreshFailure finalizationFailure
                            ? GameViewRecordingStartServiceResult.Failure(
                                finalizationFailure.Error,
                                finalizationFailure.Stored.Payload)
                            : GameViewRecordingStartServiceResult.Success(finalized.Stored.Payload);
                    }
                    if (mayHavePriorDispatch
                        && startExecution.Response.Errors[0].Code
                            is var observationCode
                        && (observationCode == GameViewRecordingErrorCodes.BindingMismatch
                            || observationCode == GameViewRecordingErrorCodes.DispatchDeadlineExceeded))
                    {
                        continuation = StartDispatchContinuation.ObserveExisting();
                    }
                    else
                    {
                        return GameViewRecordingStartServiceResult.Failure(
                            failure,
                            (await ReadLatestAsync(context, stored).ConfigureAwait(false)).Payload);
                    }
                }
                else
                {
                    string? validationError = null;
                    if (!IpcPayloadCodec.TryDeserialize(
                            startExecution.Response.Payload,
                            out IpcGameViewRecordingStartResponse response,
                            out var payloadError)
                        || !TryValidateSnapshot(stored, response.Recording, out validationError))
                    {
                        return GameViewRecordingStartServiceResult.Failure(
                            InvalidRuntimePayload(payloadError.Message, validationError),
                            (await ReadLatestAsync(context, stored).ConfigureAwait(false)).Payload);
                    }

                    continuation = StartDispatchContinuation.Accepted(response.Recording);
                }
            }
            finally
            {
                admissionLease.Dispose();
                admissionLease = null;
            }
            if (continuation is ObserveExistingStartDispatch)
            {
                return await ObserveExistingStartAsync(
                        context,
                        stored,
                        detach,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var acceptedSnapshot = continuation is AcceptedStartDispatch accepted
                ? accepted.Snapshot
                : throw new InvalidOperationException("Recording start dispatch continuation kind is not defined.");

            var applied = await ApplySnapshotAsync(
                    context,
                    lease,
                    stored,
                    acceptedSnapshot,
                    startTimeout,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (applied is RecordingRefreshFailure applicationFailure)
            {
                return GameViewRecordingStartServiceResult.Failure(
                    applicationFailure.Error,
                    (await ReadLatestAsync(context, applicationFailure.Stored).ConfigureAwait(false)).Payload);
            }

            stored = applied.Stored;
            if (!stored.Payload.IsTerminal
                && !deadline.TryGetRemainingTimeout(out _))
            {
                return MonitoringTimeout(stored.Payload);
            }
            if (detach || stored.Payload.IsTerminal)
            {
                return GameViewRecordingStartServiceResult.Success(stored.Payload);
            }

            return await MonitorAsync(context, lease, stored, deadline, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await CallerWaitCanceledAsync(context, stored).ConfigureAwait(false);
        }
        finally
        {
            admissionLease?.Dispose();
        }
    }

    public async ValueTask<GameViewRecordingStatusServiceResult> GetStatusAsync (
        GameViewRecordingStatusInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var deadlineObservedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        var deadlineObservedTimestamp = timeProvider.GetTimestamp();
        cancellationToken.ThrowIfCancellationRequested();
        if (input.RecordingId == Guid.Empty)
        {
            return Invalid<GameViewRecordingStatusPayload>("recordingId must not be the empty UUID.");
        }

        var contextResult = await projectContextResolver
            .ResolveAsync(input.ProjectPath, cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return GameViewRecordingStatusServiceResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.RecordingStatus,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return GameViewRecordingStatusServiceResult.Failure(timeoutResult.Error!);
        }

        var deadline = ExecutionDeadline.StartFromObservation(
            timeoutResult.Timeout!.Value,
            deadlineObservedAtUtc,
            deadlineObservedTimestamp,
            timeProvider);
        if (!deadline.TryGetRemainingTimeout(out var capabilityTimeout))
        {
            return await StatusTimeoutAsync(context, input.RecordingId).ConfigureAwait(false);
        }

        GameViewRecordingCapabilityResolution capabilityResolution;
        try
        {
            capabilityResolution = await capabilityResolver.ResolveAsync(
                    context,
                    UcliCommandIds.RecordingStatus,
                    capabilityTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var latest = await ReadSelectedExecutionAsync(
                    context,
                    input.RecordingId,
                    observedRuntime: null,
                    cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);
            return CallerWaitCanceled<GameViewRecordingStatusPayload>(latest?.Payload);
        }

        GameViewRecordingStoredExecution? stored;
        try
        {
            stored = await ReadSelectedExecutionAsync(
                    context,
                    input.RecordingId,
                    capabilityResolution.ObservedRuntime,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var latest = await ReadSelectedExecutionAsync(
                    context,
                    input.RecordingId,
                    capabilityResolution.ObservedRuntime,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return CallerWaitCanceled<GameViewRecordingStatusPayload>(latest?.Payload);
        }

        var capability = capabilityResolution.Capability;
        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return await StatusTimeoutAsync(context, input.RecordingId).ConfigureAwait(false);
        }

        if (stored is null)
        {
            if (input.RecordingId.HasValue)
            {
                return NotFound<GameViewRecordingStatusPayload>(input.RecordingId.Value);
            }

            return GameViewRecordingStatusServiceResult.Success(new GameViewRecordingStatusPayload(
                CreateProject(context.UnityProject),
                capability,
                new NoGameViewRecordingSelection()));
        }

        if (!stored.Payload.IsTerminal)
        {
            if (!deadline.TryGetRemainingTimeout(out var remaining))
            {
                return await StatusTimeoutAsync(context, stored.RecordingId).ConfigureAwait(false);
            }

            var opened = artifactStore.Open(context.UnityProject, stored.RecordingId);
            if (!opened.IsSuccess)
            {
                return GameViewRecordingStatusServiceResult.Failure(
                    opened.Error!,
                    stored.Payload);
            }

            RecordingRefreshResult refreshed;
            try
            {
                refreshed = await RefreshAsync(
                        context,
                        opened.Lease!,
                        stored,
                        CapTimeout(remaining),
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return await CallerWaitCanceledAsync<GameViewRecordingStatusPayload>(context, stored).ConfigureAwait(false);
            }
            stored = refreshed.Stored;
            if (refreshed is RecordingTerminalPublicationFailure publicationFailure)
            {
                return GameViewRecordingStatusServiceResult.Failure(
                    publicationFailure.Error,
                    (await ReadLatestAsync(context, stored)).Payload);
            }
            if (refreshed is RecordingRuntimeObservationFailure observationFailure)
            {
                if (observationFailure.Error.Kind == ExecutionErrorKind.Canceled)
                {
                    return await CallerWaitCanceledAsync<GameViewRecordingStatusPayload>(context, stored).ConfigureAwait(false);
                }

                if (observationFailure.Error.Code == ExecutionErrorCodes.IpcTimeout)
                {
                    return await StatusFailureAsync(
                            context,
                            stored,
                            observationFailure.Error)
                        .ConfigureAwait(false);
                }

                capability = DegradeRuntimeObservation(capability, observationFailure.Error);
            }
        }

        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return await StatusTimeoutAsync(context, stored.RecordingId).ConfigureAwait(false);
        }

        return GameViewRecordingStatusServiceResult.Success(new GameViewRecordingStatusPayload(
            CreateProject(context.UnityProject),
            capability,
            new SelectedGameViewRecordingSelection(stored.Payload)));
    }

    public async ValueTask<GameViewRecordingStopServiceResult> StopAsync (
        GameViewRecordingStopInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var deadlineObservedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        var deadlineObservedTimestamp = timeProvider.GetTimestamp();
        cancellationToken.ThrowIfCancellationRequested();
        if (input.RecordingId == Guid.Empty)
        {
            return Invalid<GameViewRecordingStopResultPayload>("recordingId must not be the empty UUID.");
        }

        var contextResult = await projectContextResolver
            .ResolveAsync(input.ProjectPath, cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return GameViewRecordingStopServiceResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.RecordingStop,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return GameViewRecordingStopServiceResult.Failure(timeoutResult.Error!);
        }

        var deadline = ExecutionDeadline.StartFromObservation(
            timeoutResult.Timeout!.Value,
            deadlineObservedAtUtc,
            deadlineObservedTimestamp,
            timeProvider);
        GameViewRecordingStoredExecution? stored;
        try
        {
            stored = await executionStore.ReadAsync(
                    context.UnityProject,
                    input.RecordingId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var latest = await executionStore.ReadAsync(
                    context.UnityProject,
                    input.RecordingId,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return CallerWaitCanceled<GameViewRecordingStopResultPayload>(latest?.Payload);
        }

        if (stored is null)
        {
            return NotFound<GameViewRecordingStopResultPayload>(input.RecordingId);
        }
        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return await StopTimeoutAsync(context, stored).ConfigureAwait(false);
        }
        if (stored.Payload.TryGetTerminal(out var terminalPayload))
        {
            return GameViewRecordingStopServiceResult.Success(terminalPayload);
        }

        var opened = artifactStore.Open(context.UnityProject, stored.RecordingId);
        if (!opened.IsSuccess)
        {
            return GameViewRecordingStopServiceResult.Failure(opened.Error!, stored.Payload);
        }

        if (!deadline.TryGetRemainingTimeout(out var statusTimeout))
        {
            return await StopTimeoutAsync(context, stored).ConfigureAwait(false);
        }

        RecordingRefreshResult refreshed;
        try
        {
            refreshed = await RefreshAsync(
                    context,
                    opened.Lease!,
                    stored,
                    CapTimeout(statusTimeout),
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await CallerWaitCanceledAsync<GameViewRecordingStopResultPayload>(context, stored).ConfigureAwait(false);
        }

        stored = refreshed.Stored;
        if (refreshed is RecordingRefreshFailure refreshFailure)
        {
            if (refreshFailure.Error.Kind == ExecutionErrorKind.Canceled)
            {
                return await CallerWaitCanceledAsync<GameViewRecordingStopResultPayload>(context, stored).ConfigureAwait(false);
            }

            return await StopFailureAsync(context, stored, refreshFailure.Error).ConfigureAwait(false);
        }
        if (stored.Payload.TryGetTerminal(out terminalPayload))
        {
            return GameViewRecordingStopServiceResult.Success(terminalPayload);
        }
        if (stored.RuntimeSnapshot is { } terminalSnapshot
            && terminalSnapshot.IsTerminal)
        {
            return stored.Payload.Lifecycle == ExecutionLifecycle.Recovery
                ? GameViewRecordingStopServiceResult.Success(
                    GameViewRecordingPayloadFactory.RequireStopResult(stored.Payload))
                : TerminalPublicationPending(stored.Payload);
        }
        if (!deadline.TryGetRemainingTimeout(out var stopTimeout))
        {
            return await StopTimeoutAsync(context, stored).ConfigureAwait(false);
        }

        UnityRequestExecutionResult execution;
        try
        {
            execution = await unityRequestExecutor.ExecuteAsync(
                    UcliCommandIds.RecordingStop,
                    UnityExecutionMode.Daemon,
                    stopTimeout,
                    context.Config,
                    context.UnityProject,
                    new UnityRequestPayload.RecordingStop(
                        new IpcGameViewRecordingStopRequest(
                            stored.RecordingId,
                            stored.RequestDigest,
                            stored.Request.MaxDurationSeconds,
                            stored.StartBinding,
                            stored.StartDispatchDeadlineUtc,
                            stored.RuntimeSnapshot)),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await CallerWaitCanceledAsync<GameViewRecordingStopResultPayload>(context, stored).ConfigureAwait(false);
        }
        if (!execution.IsSuccess)
        {
            var executionError = CreateRuntimeError(execution.FailureInfo!);
            if (executionError.Kind == ExecutionErrorKind.Canceled)
            {
                return await CallerWaitCanceledAsync<GameViewRecordingStopResultPayload>(context, stored).ConfigureAwait(false);
            }

            return await StopFailureAsync(context, stored, executionError).ConfigureAwait(false);
        }
        if (execution.Response!.Errors.Count != 0)
        {
            var executionError = CreateRuntimeError(execution.Response.Errors[0]);
            if (executionError.Kind == ExecutionErrorKind.Canceled)
            {
                return await CallerWaitCanceledAsync<GameViewRecordingStopResultPayload>(context, stored).ConfigureAwait(false);
            }

            return await StopFailureAsync(context, stored, executionError).ConfigureAwait(false);
        }
        string? validationError = null;
        if (!IpcPayloadCodec.TryDeserialize(
                execution.Response.Payload,
                out IpcGameViewRecordingStopResponse response,
                out var payloadError)
            || !TryValidateSnapshot(stored, response.Recording, out validationError))
        {
            return await StopFailureAsync(
                    context,
                    stored,
                    InvalidRuntimePayload(payloadError.Message, validationError))
                .ConfigureAwait(false);
        }

        RecordingRefreshResult applied;
        try
        {
            applied = await ApplySnapshotAsync(
                    context,
                    opened.Lease!,
                    stored,
                    response.Recording,
                    stopTimeout,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await CallerWaitCanceledAsync<GameViewRecordingStopResultPayload>(context, stored).ConfigureAwait(false);
        }
        if (applied is RecordingRefreshFailure applicationFailure)
        {
            return await StopFailureAsync(
                    context,
                    applicationFailure.Stored,
                    applicationFailure.Error)
                .ConfigureAwait(false);
        }

        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return await StopTimeoutAsync(context, applied.Stored).ConfigureAwait(false);
        }

        return applied.Stored.Payload.Lifecycle == ExecutionLifecycle.Active
            ? TerminalPublicationPending(applied.Stored.Payload)
            : GameViewRecordingStopServiceResult.Success(
                GameViewRecordingPayloadFactory.RequireStopResult(applied.Stored.Payload));
    }

    private async ValueTask<GameViewRecordingStartServiceResult> ResolveRepeatedStartAsync (
        ProjectContext context,
        GameViewRecordingRequestDocument requested,
        GameViewRecordingStoredExecution existing,
        bool detach,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(requested, existing.StartLimits);
        if (!normalized.IsSuccess || normalized.Request!.Digest != existing.RequestDigest)
        {
            return IdConflict(existing.RecordingId);
        }

        if (existing.Payload.IsTerminal)
        {
            return GameViewRecordingStartServiceResult.Success(existing.Payload);
        }

        if ((existing.RuntimeSnapshot is null
                || existing.RuntimeSnapshot.State == GameViewRecordingState.Preparing)
            && TryGetStartDispatchTimeout(existing, out _))
        {
            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return await MonitoringTimeoutAsync(context, existing).ConfigureAwait(false);
            }

            var opened = artifactStore.Open(context.UnityProject, existing.RecordingId);
            if (!opened.IsSuccess)
            {
                return GameViewRecordingStartServiceResult.Failure(
                    opened.Error!,
                    existing.Payload);
            }

            return await StartRegisteredAsync(
                    context,
                    opened.Lease!,
                    existing,
                    detach,
                    mayHavePriorDispatch: true,
                    deadline,
                    admissionLease: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await ObserveExistingStartAsync(
                context,
                existing,
                detach,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<GameViewRecordingStartServiceResult> ObserveExistingStartAsync (
        ProjectContext context,
        GameViewRecordingStoredExecution existing,
        bool detach,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        try
        {
            if (existing.Payload.IsTerminal)
            {
                return GameViewRecordingStartServiceResult.Success(existing.Payload);
            }

            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return await MonitoringTimeoutAsync(context, existing).ConfigureAwait(false);
            }

            var opened = artifactStore.Open(context.UnityProject, existing.RecordingId);
            if (!opened.IsSuccess)
            {
                return GameViewRecordingStartServiceResult.Failure(
                    opened.Error!,
                    existing.Payload);
            }
            if (!deadline.TryGetRemainingTimeout(out var timeout))
            {
                return MonitoringTimeout(existing.Payload);
            }

            var refreshed = await RefreshAsync(
                    context,
                    opened.Lease!,
                    existing,
                    CapTimeout(timeout),
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            var stored = refreshed.Stored;
            if (refreshed is RecordingTerminalPublicationFailure publicationFailure)
            {
                return GameViewRecordingStartServiceResult.Failure(
                    publicationFailure.Error,
                    (await ReadLatestAsync(context, stored).ConfigureAwait(false)).Payload);
            }
            if (refreshed is RecordingRuntimeObservationFailure
                {
                    Error.Kind: ExecutionErrorKind.Canceled,
                } cancellationFailure)
            {
                return GameViewRecordingStartServiceResult.Failure(
                    cancellationFailure.Error,
                    (await ReadLatestAsync(context, stored).ConfigureAwait(false)).Payload);
            }
            if (detach || stored.Payload.IsTerminal)
            {
                return GameViewRecordingStartServiceResult.Success(stored.Payload);
            }

            return await MonitorAsync(
                    context,
                    opened.Lease!,
                    stored,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await CallerWaitCanceledAsync(context, existing).ConfigureAwait(false);
        }
    }

    private async ValueTask<GameViewRecordingStartServiceResult> MonitorAsync (
        ProjectContext context,
        IGameViewRecordingArtifactLease lease,
        GameViewRecordingStoredExecution stored,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        while (!stored.Payload.IsTerminal)
        {
            if (!deadline.TryGetRemainingTimeout(out var remaining))
            {
                return MonitoringTimeout(stored.Payload);
            }

            var delay = remaining < MonitoringPollInterval
                ? remaining
                : MonitoringPollInterval;
            await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
            if (!deadline.TryGetRemainingTimeout(out remaining))
            {
                return MonitoringTimeout(stored.Payload);
            }

            var refreshed = await RefreshAsync(
                    context,
                    lease,
                    stored,
                    CapTimeout(remaining),
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            stored = refreshed.Stored;
            if (refreshed is RecordingTerminalPublicationFailure publicationFailure)
            {
                return GameViewRecordingStartServiceResult.Failure(
                    publicationFailure.Error,
                    (await ReadLatestAsync(context, stored).ConfigureAwait(false)).Payload);
            }
            if (refreshed is RecordingRuntimeObservationFailure
                {
                    Error.Kind: ExecutionErrorKind.Canceled,
                })
            {
                return await CallerWaitCanceledAsync(context, stored).ConfigureAwait(false);
            }
        }

        return GameViewRecordingStartServiceResult.Success(stored.Payload);
    }

    private async ValueTask<RecordingRefreshResult> RefreshAsync (
        ProjectContext context,
        IGameViewRecordingArtifactLease lease,
        GameViewRecordingStoredExecution stored,
        TimeSpan timeout,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (stored.RuntimeSnapshot is { } knownTerminal
            && knownTerminal.IsTerminal)
        {
            return await ApplySnapshotAsync(
                    context,
                    lease,
                    stored,
                    knownTerminal,
                    timeout,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var execution = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.RecordingStatus,
                UnityExecutionMode.Daemon,
                timeout,
                context.Config,
                context.UnityProject,
                new UnityRequestPayload.RecordingStatus(
                    new IpcGameViewRecordingStatusRequest(
                        stored.RecordingId,
                        stored.RequestDigest,
                        stored.Request.MaxDurationSeconds,
                        stored.StartBinding,
                        stored.StartDispatchDeadlineUtc,
                        stored.RuntimeSnapshot)),
                cancellationToken)
            .ConfigureAwait(false);
        if (!execution.IsSuccess)
        {
            return await RecoverRuntimeObservationFailureAsync(
                    context,
                    lease,
                    stored,
                    timeout,
                    CreateRuntimeError(execution.FailureInfo!),
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (execution.Response!.Errors.Count != 0)
        {
            return await RecoverRuntimeObservationFailureAsync(
                    context,
                    lease,
                    stored,
                    timeout,
                    CreateRuntimeError(execution.Response.Errors[0]),
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (!IpcPayloadCodec.TryDeserialize(
                execution.Response.Payload,
                out IpcGameViewRecordingStatusResponse response,
                out var payloadError))
        {
            return await RecoverRuntimeObservationFailureAsync(
                    context,
                    lease,
                    stored,
                    timeout,
                    InvalidRuntimePayload(payloadError.Message, validationError: null),
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (response.RecordingSelection is not IpcSelectedGameViewRecordingSelection selected)
        {
            // NOTE: A missing selection after this deadline proves that no adapter-owned start was observed.
            if (HasUnobservedStartDispatchDeadline(stored))
            {
                return await FinalizeDispatchDeadlineExceededAsync(
                        context,
                        lease,
                        stored,
                        timeout,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return await RecoverRuntimeObservationFailureAsync(
                    context,
                    lease,
                    stored,
                    timeout,
                    ExecutionError.InternalError(
                        "Unity no longer reports the registered GameView recording.",
                        GameViewRecordingErrorCodes.Interrupted),
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (!TryValidateSnapshot(stored, selected.Recording, out var validationError))
        {
            return await RecoverRuntimeObservationFailureAsync(
                    context,
                    lease,
                    stored,
                    timeout,
                    InvalidRuntimePayload(payloadError.Message, validationError),
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await ApplySnapshotAsync(
                context,
                lease,
                stored,
                selected.Recording,
                timeout,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<RecordingRefreshResult> FinalizeDispatchDeadlineExceededAsync (
        ProjectContext context,
        IGameViewRecordingArtifactLease lease,
        GameViewRecordingStoredExecution stored,
        TimeSpan terminalPublicationWaitTimeout,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken) =>
        await ApplySnapshotAsync(
                context,
                lease,
                stored,
                CreateDispatchDeadlineExceededSnapshot(stored),
                terminalPublicationWaitTimeout,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);

    private IpcGameViewRecordingIndeterminateSnapshot CreateDispatchDeadlineExceededSnapshot (
        GameViewRecordingStoredExecution stored)
    {
        var previous = stored.RuntimeSnapshot;
        var observedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        if (observedAtUtc < stored.StartDispatchDeadlineUtc)
        {
            observedAtUtc = stored.StartDispatchDeadlineUtc;
        }
        if (previous is not null && observedAtUtc < previous.UpdatedAtUtc)
        {
            observedAtUtc = previous.UpdatedAtUtc;
        }
        if (previous?.ObservedStartedAtUtc is DateTimeOffset startedAtUtc
            && observedAtUtc < startedAtUtc)
        {
            observedAtUtc = startedAtUtc;
        }

        return new IpcGameViewRecordingIndeterminateSnapshot(
            stored.RecordingId,
            stored.RequestDigest,
            GameViewRecordingState.Indeterminate,
            GameViewRecordingStopReason.InternalFailure,
            new IpcError(
                GameViewRecordingErrorCodes.DispatchDeadlineExceeded,
                "The recording start dispatch deadline elapsed without an adapter-owned observation.",
                InstancePath: null),
            stored.StartBinding.Runtime,
            cleanup: null,
            previous?.ObservedTarget,
            timing: null,
            stored.Request.MaxDurationSeconds,
            previous?.EncodedFrameCount,
            previous?.ObservedStartedAtUtc,
            previous?.ObservedStopRequestedAtUtc,
            observedAtUtc,
            observedAtUtc,
            stored.StartBinding.Generation,
            previous?.ObservedGeneration ?? stored.StartBinding.Generation);
    }

    private async ValueTask<RecordingRefreshResult> RecoverRuntimeObservationFailureAsync (
        ProjectContext context,
        IGameViewRecordingArtifactLease lease,
        GameViewRecordingStoredExecution stored,
        TimeSpan timeout,
        ExecutionError observationError,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (observationError.Kind == ExecutionErrorKind.Canceled
            || cancellationToken.IsCancellationRequested
            || processIdentityObserver.Observe(stored.StartBinding.Process)
                != ProcessIdentityStatus.ExitedOrReplaced)
        {
            return RecordingRefreshResult.RuntimeObservationFailure(stored, observationError);
        }

        var previous = stored.RuntimeSnapshot;
        var observedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        if (previous is not null && observedAtUtc < previous.UpdatedAtUtc)
        {
            observedAtUtc = previous.UpdatedAtUtc;
        }
        if (previous?.ObservedStartedAtUtc is DateTimeOffset startedAtUtc
            && observedAtUtc < startedAtUtc)
        {
            observedAtUtc = startedAtUtc;
        }

        var snapshot = IpcGameViewRecordingSnapshot.Create(
            stored.RecordingId,
            stored.RequestDigest,
            GameViewRecordingState.Indeterminate,
            previous?.ObservedStopReason ?? GameViewRecordingStopReason.UnityExited,
            new IpcError(
                GameViewRecordingErrorCodes.Interrupted,
                "The Unity Editor process that owned the GameView recording exited or was replaced.",
                InstancePath: null),
            stored.StartBinding.Runtime,
            cleanup: null,
            previous?.ObservedTarget,
            timing: null,
            stored.Request.MaxDurationSeconds,
            previous?.EncodedFrameCount,
            previous?.ObservedStartedAtUtc,
            previous?.ObservedStopRequestedAtUtc,
            observedAtUtc,
            observedAtUtc,
            stored.StartBinding.Generation,
            previous?.ObservedGeneration ?? stored.StartBinding.Generation);
        return await ApplySnapshotAsync(
                context,
                lease,
                stored,
                snapshot,
                timeout,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<RecordingRefreshResult> ApplySnapshotAsync (
        ProjectContext context,
        IGameViewRecordingArtifactLease lease,
        GameViewRecordingStoredExecution stored,
        IpcGameViewRecordingSnapshot snapshot,
        TimeSpan terminalPublicationWaitTimeout,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!snapshot.TryGetTerminal(out var terminalSnapshot))
        {
            var observedPayload = GameViewRecordingPayloadFactory.CreateObservedNonTerminal(stored, snapshot);
            var observed = CopyStored(stored, snapshot, observedPayload);
            var observationExchange = await executionStore.CompareExchangeAsync(
                    context.UnityProject,
                    lease.ExecutionStatePath,
                    stored,
                    observed,
                    cancellationToken)
                .ConfigureAwait(false);
            return RecordingRefreshResult.Success(observationExchange.Current);
        }

        // A terminal runtime fact must be recoverable before a publisher can own its artifact stages.
        var current = stored;
        while (!current.Payload.IsTerminal)
        {
            if (!TryValidateSnapshot(current, terminalSnapshot, out _))
            {
                return ResolveTerminalPublicationCheckpoint(current);
            }

            var observedPayload = GameViewRecordingPayloadFactory.CreateObservedNonTerminal(
                current,
                terminalSnapshot);
            var observed = CopyStored(current, terminalSnapshot, observedPayload);
            var observationExchange = await executionStore.CompareExchangeAsync(
                    context.UnityProject,
                    lease.ExecutionStatePath,
                    current,
                    observed,
                    CancellationToken.None)
                .ConfigureAwait(false);
            current = observationExchange.Current;
            if (observationExchange.Exchanged)
            {
                break;
            }
        }
        if (current.Payload.IsTerminal)
        {
            return RecordingRefreshResult.Success(current);
        }

        if (!deadline.TryGetRemainingTimeout(out var remaining))
        {
            return RecordingRefreshResult.Success(current);
        }

        using var publicationLease = await executionStore.TryAcquireTerminalPublicationLeaseAsync(
                context.UnityProject,
                stored.RecordingId,
                CapTimeout(remaining < terminalPublicationWaitTimeout
                    ? remaining
                    : terminalPublicationWaitTimeout),
                cancellationToken)
            .ConfigureAwait(false);
        if (publicationLease is null)
        {
            var durable = await executionStore.ReadAsync(
                    context.UnityProject,
                    stored.RecordingId,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return ResolveTerminalPublicationCheckpoint(durable ?? current);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return RecordingRefreshResult.RuntimeObservationFailure(
                current,
                CallerCancellationError());
        }

        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return RecordingRefreshResult.Success(current);
        }

        var finalization = await terminalFinalizer.FinalizeAsync(
                context,
                lease,
                current,
                terminalSnapshot,
                () => deadline.TryGetRemainingTimeout(out _),
                CancellationToken.None)
            .ConfigureAwait(false);
        if (finalization is GameViewRecordingTerminalFinalizationFailure finalizationFailure)
        {
            var recovery = CopyStored(
                current,
                terminalSnapshot,
                finalizationFailure.RecoveryPayload);
            return RecordingRefreshResult.TerminalPublicationFailure(
                recovery,
                deadline.IsExpired
                    ? CreateTerminalPublicationPendingError()
                    : finalizationFailure.Error);
        }

        var finalizationSuccess = finalization as GameViewRecordingTerminalFinalizationSuccess
            ?? throw new InvalidOperationException("Recording terminal finalization result kind is not defined.");

        var latest = await executionStore.ReadAsync(
                context.UnityProject,
                current.RecordingId,
                CancellationToken.None)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "Recording execution state disappeared during terminal publication.");
        if (latest.Payload.IsTerminal)
        {
            return RecordingRefreshResult.Success(latest);
        }

        var terminal = CopyStored(latest, terminalSnapshot, finalizationSuccess.Payload);
        var terminalExchange = await executionStore.CompareExchangeAsync(
                context.UnityProject,
                lease.ExecutionStatePath,
                latest,
                terminal,
                CancellationToken.None)
            .ConfigureAwait(false);
        return cancellationToken.IsCancellationRequested
            ? RecordingRefreshResult.RuntimeObservationFailure(
                terminalExchange.Current,
                CallerCancellationError())
            : ResolveTerminalPublicationCheckpoint(terminalExchange.Current);
    }

    private static GameViewRecordingRequestNormalizationResult Normalize (
        GameViewRecordingRequestDocument request,
        GameViewRecordingLimits limits)
    {
        return GameViewRecordingRequestNormalizer.Normalize(
            request,
            limits.MinimumWidth,
            limits.MaximumWidth,
            limits.MinimumHeight,
            limits.MaximumHeight,
            limits.DimensionMultiple,
            limits.MinimumFrameRate,
            limits.MaximumFrameRate,
            limits.DefaultMaxDurationSeconds,
            limits.MaximumMaxDurationSeconds);
    }

    private static GameViewRecordingCapability DegradeRuntimeObservation (
        GameViewRecordingCapability capability,
        ExecutionError observationError) =>
        new(
            capability.Package,
            capability.Compatibility,
            capability.Adapter,
            new GameViewRecordingRuntimeAdmission(
                GameViewRecordingRuntimeAdmissionState.Unobserved,
                [observationError.Code ?? GameViewRecordingErrorCodes.AdapterFaulted]),
            capability.Limits,
            capability.CaptureProfile);

    private static bool TryValidateSnapshot (
        GameViewRecordingStoredExecution stored,
        IpcGameViewRecordingSnapshot snapshot,
        out string? validationError)
    {
        if (snapshot.RecordingId != stored.RecordingId
            || snapshot.RequestDigest != stored.RequestDigest
            || snapshot.Runtime != stored.StartBinding.Runtime
            || snapshot.StartGeneration != stored.StartBinding.Generation)
        {
            validationError = "recording identity or runtime binding does not match the durable start record";
            return false;
        }
        if (snapshot.EffectiveMaxDurationSeconds != stored.Request.MaxDurationSeconds)
        {
            validationError = "effective maximum duration does not match the durable request";
            return false;
        }
        if (snapshot.ObservedTarget is { } target
            && target.RequestedDimensions != stored.Request.Resolution)
        {
            validationError = "runtime target does not match the durable requested dimensions";
            return false;
        }
        if (stored.RuntimeSnapshot is { } previous)
        {
            if (!IsRuntimeTransitionAllowed(previous.State, snapshot.State)
                || snapshot.StartGeneration != previous.StartGeneration
                || snapshot.Runtime != previous.Runtime
                || (previous.ObservedTarget is not null
                    && snapshot.ObservedTarget != previous.ObservedTarget)
                || (previous.ObservedStopReason.HasValue
                    && snapshot.ObservedStopReason != previous.ObservedStopReason)
                || snapshot.UpdatedAtUtc < previous.UpdatedAtUtc
                || (previous.ObservedStartedAtUtc.HasValue
                    && snapshot.ObservedStartedAtUtc != previous.ObservedStartedAtUtc)
                || (previous.ObservedStopRequestedAtUtc.HasValue
                    && snapshot.ObservedStopRequestedAtUtc != previous.ObservedStopRequestedAtUtc)
                || (previous.EncodedFrameCount.HasValue
                    && (!snapshot.EncodedFrameCount.HasValue
                        || snapshot.EncodedFrameCount.Value < previous.EncodedFrameCount.Value)))
            {
                validationError = "runtime progress regressed or changed an established recording fact";
                return false;
            }
        }

        validationError = null;
        return true;
    }

    private static bool IsRuntimeTransitionAllowed (
        GameViewRecordingState previous,
        GameViewRecordingState next)
    {
        if (previous == next)
        {
            return true;
        }

        return previous switch
        {
            GameViewRecordingState.Preparing => true,
            GameViewRecordingState.Recording => next is GameViewRecordingState.Finalizing
                or GameViewRecordingState.Completed
                or GameViewRecordingState.Failed
                or GameViewRecordingState.Indeterminate,
            GameViewRecordingState.Finalizing => next is GameViewRecordingState.Completed
                or GameViewRecordingState.Failed
                or GameViewRecordingState.Indeterminate,
            GameViewRecordingState.Completed
                or GameViewRecordingState.Failed
                or GameViewRecordingState.Indeterminate => false,
            _ => false,
        };
    }

    private static GameViewRecordingStoredExecution CreateStored (
        Guid recordingId,
        GameViewRecordingEffectiveRequest effective,
        PathArtifactRef requestRef,
        GameViewRecordingCapability capability,
        IpcGameViewRecordingStartBinding startBinding,
        DateTimeOffset startDispatchDeadlineUtc,
        IpcGameViewRecordingSnapshot? runtimeSnapshot,
        GameViewRecordingExecutionPayload payload) =>
        new(
            GameViewRecordingStoredExecution.CurrentSchemaVersion,
            recordingId,
            ToContractRequest(effective),
            effective.CanonicalJson,
            effective.Digest,
            requestRef,
            capability,
            startBinding,
            startDispatchDeadlineUtc,
            runtimeSnapshot,
            payload);

    private static GameViewRecordingStoredExecution CopyStored (
        GameViewRecordingStoredExecution source,
        IpcGameViewRecordingSnapshot snapshot,
        GameViewRecordingExecutionPayload payload) =>
        new(
            source.SchemaVersion,
            source.RecordingId,
            source.Request,
            source.CanonicalRequestJson,
            source.RequestDigest,
            source.RequestRef,
            source.StartCapability,
            source.StartBinding,
            source.StartDispatchDeadlineUtc,
            snapshot,
            payload);

    private static GameViewRecordingRequest ToContractRequest (
        GameViewRecordingEffectiveRequest request) =>
        new(
            request.SchemaVersion,
            request.Resolution,
            request.FrameRate,
            request.MaxDurationSeconds);

    private static UnityProjectIdentity CreateProject (ResolvedUnityProjectContext project) =>
        new(project.UnityProjectRoot.Value, project.ProjectFingerprint, project.UnityVersion);

    private ValueTask<GameViewRecordingStoredExecution?> ReadSelectedExecutionAsync (
        ProjectContext context,
        Guid? recordingId,
        GameViewRecordingRuntimeIdentity? observedRuntime,
        CancellationToken cancellationToken)
    {
        if (recordingId.HasValue)
        {
            return executionStore.ReadAsync(
                context.UnityProject,
                recordingId.Value,
                cancellationToken);
        }

        return observedRuntime is null
            ? ValueTask.FromResult<GameViewRecordingStoredExecution?>(null)
            : executionStore.ReadCurrentAsync(
                context.UnityProject,
                observedRuntime.RuntimeId,
                cancellationToken);
    }

    private static TimeSpan CapTimeout (TimeSpan timeout) =>
        timeout < RuntimeStatusRequestLimit ? timeout : RuntimeStatusRequestLimit;

    private static TimeSpan CapStartDispatchTimeout (TimeSpan timeout) =>
        timeout < StartDispatchRequestLimit ? timeout : StartDispatchRequestLimit;

    private bool TryGetStartDispatchTimeout (
        GameViewRecordingStoredExecution stored,
        out TimeSpan timeout)
    {
        var remaining = stored.StartDispatchDeadlineUtc
            - timeProvider.GetUtcNow().ToUniversalTime();
        if (remaining <= TimeSpan.Zero)
        {
            timeout = default;
            return false;
        }

        timeout = CapStartDispatchTimeout(remaining);
        return true;
    }

    private bool HasUnobservedStartDispatchDeadline (
        GameViewRecordingStoredExecution stored) =>
        (stored.RuntimeSnapshot is null
            || stored.RuntimeSnapshot.State == GameViewRecordingState.Preparing)
        && timeProvider.GetUtcNow().ToUniversalTime() >= stored.StartDispatchDeadlineUtc;

    private static ExecutionError CreateRuntimeError (UnityRequestFailure failure) =>
        failure.Code == ExecutionErrorCodes.Canceled
            ? ExecutionError.Canceled(failure.Message, failure.Code)
            : failure.Code == ExecutionErrorCodes.IpcTimeout
                ? ExecutionError.Timeout(failure.Message, failure.Code)
                : ExecutionError.InternalError(failure.Message, failure.Code);

    private static ExecutionError CreateRuntimeError (OperationExecutionError failure) =>
        failure.Code == ExecutionErrorCodes.Canceled
            ? ExecutionError.Canceled(failure.Message, failure.Code)
            : failure.Code == ExecutionErrorCodes.IpcTimeout
                ? ExecutionError.Timeout(failure.Message, failure.Code)
                : ExecutionError.InternalError(failure.Message, failure.Code);

    private static ExecutionError CallerCancellationError () =>
        ExecutionError.Canceled(
            "Waiting for the GameView recording was canceled; the returned execution checkpoint, when present, reports the latest durable state.",
            ExecutionErrorCodes.Canceled);

    private static ExecutionError InvalidRuntimePayload (
        string? codecError,
        string? validationError)
    {
        var detail = validationError ?? codecError ?? "unknown payload error";
        return ExecutionError.InternalError(
            $"Unity GameView recording payload is invalid: {detail}.",
            GameViewRecordingErrorCodes.AdapterFaulted);
    }

    private static GameViewRecordingStartServiceResult Invalid (string message) =>
        Invalid<GameViewRecordingExecutionPayload>(message);

    private static GameViewRecordingServiceResult<TPayload> Invalid<TPayload> (
        string message)
        where TPayload : GameViewRecordingPayload =>
        GameViewRecordingServiceResult<TPayload>.Failure(
            ExecutionError.InvalidArgument(message, UcliCoreErrorCodes.InvalidArgument));

    private static GameViewRecordingStartServiceResult IdConflict (Guid recordingId) =>
        GameViewRecordingStartServiceResult.Failure(
            ExecutionError.InternalError(
                $"Recording id {recordingId:D} is already bound to a different effective request.",
                GameViewRecordingErrorCodes.IdConflict));

    private static GameViewRecordingStartServiceResult Conflict (Guid recordingId) =>
        GameViewRecordingStartServiceResult.Failure(
            ExecutionError.InternalError(
                $"Recording {recordingId:D} already owns the GameView recording exclusion.",
                GameViewRecordingErrorCodes.Conflict));

    private static GameViewRecordingStartServiceResult NotFound (Guid recordingId) =>
        NotFound<GameViewRecordingExecutionPayload>(recordingId);

    private static GameViewRecordingServiceResult<TPayload> NotFound<TPayload> (
        Guid recordingId)
        where TPayload : GameViewRecordingPayload =>
        GameViewRecordingServiceResult<TPayload>.Failure(
            ExecutionError.InternalError(
                $"Recording {recordingId:D} was not found.",
                GameViewRecordingErrorCodes.NotFound));

    private static GameViewRecordingStartServiceResult MonitoringTimeout (
        GameViewRecordingExecutionPayload? payload) =>
        GameViewRecordingStartServiceResult.Failure(
            ExecutionError.Timeout(
                "GameView recording monitoring reached its deadline before this caller observed a terminal result; inspect the returned durable checkpoint.",
                GameViewRecordingErrorCodes.MonitoringTimeout),
            payload);

    private async ValueTask<GameViewRecordingStartServiceResult> MonitoringTimeoutAsync (
        ProjectContext context,
        GameViewRecordingStoredExecution known)
    {
        var latest = await ReadLatestAsync(context, known).ConfigureAwait(false);
        return MonitoringTimeout(latest.Payload);
    }

    private static GameViewRecordingStartServiceResult StartAdmissionTimeout () =>
        GameViewRecordingStartServiceResult.Failure(
            ExecutionError.Timeout(
                "GameView recording start could not establish a durable execution within the caller's deadline.",
                ExecutionErrorCodes.IpcTimeout));

    private static GameViewRecordingStopServiceResult StopTimeout (
        GameViewRecordingExecutionPayload payload) =>
        GameViewRecordingStopServiceResult.Failure(
            ExecutionError.Timeout(
                "GameView recording stop could not complete within the caller's deadline.",
                ExecutionErrorCodes.IpcTimeout),
            payload);

    private async ValueTask<GameViewRecordingStatusServiceResult> StatusTimeoutAsync (
        ProjectContext context,
        Guid? recordingId)
    {
        if (!recordingId.HasValue)
        {
            return GameViewRecordingStatusServiceResult.Failure(
                ExecutionError.Timeout(
                    "GameView recording status could not complete within the caller's deadline.",
                    ExecutionErrorCodes.IpcTimeout));
        }

        var latest = await executionStore.ReadAsync(
                context.UnityProject,
                recordingId.Value,
                CancellationToken.None)
            .ConfigureAwait(false);
        return GameViewRecordingStatusServiceResult.Failure(
            ExecutionError.Timeout(
                "GameView recording status could not complete within the caller's deadline.",
                ExecutionErrorCodes.IpcTimeout),
            latest?.Payload);
    }

    private async ValueTask<GameViewRecordingStopServiceResult> StopTimeoutAsync (
        ProjectContext context,
        GameViewRecordingStoredExecution known) =>
        await StopFailureAsync(
                context,
                known,
                ExecutionError.Timeout(
                    "GameView recording stop could not complete within the caller's deadline.",
                    ExecutionErrorCodes.IpcTimeout))
            .ConfigureAwait(false);

    private async ValueTask<GameViewRecordingStatusServiceResult> StatusFailureAsync (
        ProjectContext context,
        GameViewRecordingStoredExecution known,
        ExecutionError error)
    {
        var latest = await ReadLatestAsync(context, known).ConfigureAwait(false);
        return GameViewRecordingStatusServiceResult.Failure(error, latest.Payload);
    }

    private async ValueTask<GameViewRecordingStopServiceResult> StopFailureAsync (
        ProjectContext context,
        GameViewRecordingStoredExecution known,
        ExecutionError error)
    {
        var latest = await ReadLatestAsync(context, known).ConfigureAwait(false);
        return GameViewRecordingStopServiceResult.Failure(error, latest.Payload);
    }

    private async ValueTask<GameViewRecordingStoredExecution> ReadLatestAsync (
        ProjectContext context,
        GameViewRecordingStoredExecution known) =>
        await executionStore.ReadAsync(
                context.UnityProject,
                known.RecordingId,
                CancellationToken.None)
            .ConfigureAwait(false)
        ?? known;

    private static GameViewRecordingStopServiceResult TerminalPublicationPending (
        GameViewRecordingExecutionPayload payload) =>
        GameViewRecordingStopServiceResult.Failure(
            CreateTerminalPublicationPendingError(),
            payload);

    private static RecordingRefreshResult ResolveTerminalPublicationCheckpoint (
        GameViewRecordingStoredExecution stored) =>
        stored.Payload.Lifecycle == ExecutionLifecycle.Active
            ? RecordingRefreshResult.TerminalPublicationFailure(
                stored,
                CreateTerminalPublicationPendingError())
            : RecordingRefreshResult.Success(stored);

    private static ExecutionError CreateTerminalPublicationPendingError () =>
        ExecutionError.Timeout(
            "GameView recording terminal publication did not establish a recovery or terminal checkpoint within the current observation attempt.",
            ExecutionErrorCodes.IpcTimeout);

    private async ValueTask<GameViewRecordingStartServiceResult> CallerWaitCanceledAsync (
        ProjectContext context,
        GameViewRecordingStoredExecution known) =>
        await CallerWaitCanceledAsync<GameViewRecordingExecutionPayload>(
                context,
                known)
            .ConfigureAwait(false);

    private async ValueTask<GameViewRecordingServiceResult<TPayload>> CallerWaitCanceledAsync<TPayload> (
        ProjectContext context,
        GameViewRecordingStoredExecution known)
        where TPayload : GameViewRecordingPayload
    {
        var latest = await executionStore.ReadAsync(
                context.UnityProject,
                known.RecordingId,
                CancellationToken.None)
            .ConfigureAwait(false);
        return CallerWaitCanceled<TPayload>((latest ?? known).Payload);
    }

    private static GameViewRecordingStartServiceResult CallerWaitCanceled (
        GameViewRecordingExecutionPayload? payload) =>
        CallerWaitCanceled<GameViewRecordingExecutionPayload>(payload);

    private static GameViewRecordingServiceResult<TPayload> CallerWaitCanceled<TPayload> (
        GameViewRecordingExecutionPayload? payload)
        where TPayload : GameViewRecordingPayload =>
        GameViewRecordingServiceResult<TPayload>.Failure(
            CallerCancellationError(),
            payload);

    private abstract record RecordingRefreshResult
    {
        protected RecordingRefreshResult (GameViewRecordingStoredExecution stored)
        {
            Stored = stored ?? throw new ArgumentNullException(nameof(stored));
        }

        public GameViewRecordingStoredExecution Stored { get; }

        public static RecordingRefreshResult Success (GameViewRecordingStoredExecution stored) =>
            new RecordingRefreshSuccess(stored);

        public static RecordingRefreshResult RuntimeObservationFailure (
            GameViewRecordingStoredExecution stored,
            ExecutionError error) =>
            new RecordingRuntimeObservationFailure(stored, error);

        public static RecordingRefreshResult TerminalPublicationFailure (
            GameViewRecordingStoredExecution stored,
            ExecutionError error) =>
            new RecordingTerminalPublicationFailure(stored, error);
    }

    private sealed record RecordingRefreshSuccess : RecordingRefreshResult
    {
        public RecordingRefreshSuccess (GameViewRecordingStoredExecution stored)
            : base(stored)
        {
        }
    }

    private abstract record RecordingRefreshFailure : RecordingRefreshResult
    {
        protected RecordingRefreshFailure (
            GameViewRecordingStoredExecution stored,
            ExecutionError error)
            : base(stored)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public ExecutionError Error { get; }
    }

    private sealed record RecordingRuntimeObservationFailure : RecordingRefreshFailure
    {
        public RecordingRuntimeObservationFailure (
            GameViewRecordingStoredExecution stored,
            ExecutionError error)
            : base(stored, error)
        {
        }
    }

    private sealed record RecordingTerminalPublicationFailure : RecordingRefreshFailure
    {
        public RecordingTerminalPublicationFailure (
            GameViewRecordingStoredExecution stored,
            ExecutionError error)
            : base(stored, error)
        {
        }
    }

    private abstract record StartDispatchContinuation
    {
        public static StartDispatchContinuation Accepted (IpcGameViewRecordingSnapshot snapshot) =>
            new AcceptedStartDispatch(snapshot);

        public static StartDispatchContinuation ObserveExisting () =>
            new ObserveExistingStartDispatch();
    }

    private sealed record AcceptedStartDispatch : StartDispatchContinuation
    {
        public AcceptedStartDispatch (IpcGameViewRecordingSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public IpcGameViewRecordingSnapshot Snapshot { get; }
    }

    private sealed record ObserveExistingStartDispatch : StartDispatchContinuation;
}
