using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Runtime;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using AttemptResolution =
    MackySoft.Ucli.Unity.Ipc.LifecycleExecutionAttemptResolution;
using TerminalPublication =
    MackySoft.Ucli.Unity.Ipc.LifecycleExecutionTerminalPublication<
        MackySoft.Ucli.Contracts.Execution.Lifecycle.RefreshLifecycleExecutionTerminalRecord>;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Owns the typed <c>refresh</c> state machine from side-effect admission through terminal publication.
    /// </summary>
    internal sealed class RefreshLifecycleExecutionHandler :
        IRefreshLifecycleExecutionHandler,
        ILifecycleExecutionRecoveryHandler
    {
        private const int RequiredStableLifecycleObservations = 2;

        private const string TerminalPublicationFailureMessage =
            "Refresh terminal record could not be published and reverified.";

        private readonly IRefreshLifecycleExecutionProvider provider;
        private readonly IDaemonLogger daemonLogger;
        private readonly FileLifecycleExecutionStore executionStore;
        private readonly LifecycleExecutionAttemptBoundary attemptBoundary;
        private readonly FileRefreshLifecycleExecutionCheckpointStore checkpointStore;
        private readonly LifecycleExecutionSideEffectAdmissionCoordinator
            sideEffectAdmission;
        private readonly LifecycleExecutionTerminalPublicationBoundary<
            RefreshLifecycleExecutionTerminalRecord>
            terminalPublication;

        public RefreshLifecycleExecutionHandler (
            IRefreshLifecycleExecutionProvider provider,
            IDaemonLogger daemonLogger,
            FileLifecycleExecutionStore executionStore,
            FileRefreshLifecycleExecutionCheckpointStore checkpointStore)
        {
            this.provider = provider
                ?? throw new ArgumentNullException(nameof(provider));
            this.daemonLogger = daemonLogger
                ?? throw new ArgumentNullException(nameof(daemonLogger));
            this.executionStore = executionStore
                ?? throw new ArgumentNullException(nameof(executionStore));
            attemptBoundary = new(this.executionStore);
            this.checkpointStore = checkpointStore
                ?? throw new ArgumentNullException(nameof(checkpointStore));
            sideEffectAdmission =
                new LifecycleExecutionSideEffectAdmissionCoordinator(
                    this.executionStore);
            terminalPublication =
                new LifecycleExecutionTerminalPublicationBoundary<
                    RefreshLifecycleExecutionTerminalRecord>(
                    Kind,
                    this.executionStore,
                    this.daemonLogger,
                    "Refresh terminal publication failed.",
                    "Refresh terminal publication failed during recovery.");
        }

        public LifecycleExecutionKind Kind => LifecycleExecutionKind.Refresh;

        public async ValueTask<RefreshLifecycleExecutionOutcome> ExecuteAsync (
            LifecycleExecutionStartBinding requestedStart)
        {
            if (requestedStart == null)
            {
                throw new ArgumentNullException(nameof(requestedStart));
            }

            var claimantEndpointRegistrationGenerationId =
                requestedStart.Host
                    .CurrentEndpointRegistrationGenerationId;
            var executionId = requestedStart.LifecycleExecutionRef.Id;
            var attemptResolution =
                await attemptBoundary.ResolveInvocationAsync(
                Kind,
                requestedStart,
                CancellationToken.None);
            if (attemptResolution is AttemptResolution.Missing)
            {
                return CreateErrorResponse(
                    UcliCoreErrorCodes.InvalidArgument,
                    "Refresh start record was not found.",
                    lifecycleExecutionRef: null,
                    ExecutionApplicationState.NotApplied,
                    result: null,
                    refresh: null,
                    observedLifecycle: CreateObservation(
                        provider.CaptureObservation()),
                    readPostcondition: null);
            }
            if (attemptResolution
                is AttemptResolution.BindingMismatch bindingMismatch)
            {
                return CreateErrorResponse(
                    LifecycleExecutionStartBindingMatcher
                        .GetMismatchErrorCode(bindingMismatch.Match),
                    "Refresh request does not match its durable start binding.",
                    lifecycleExecutionRef: null,
                    ExecutionApplicationState.NotApplied,
                    result: null,
                    refresh: null,
                    observedLifecycle: CreateObservation(
                        provider.CaptureObservation()),
                    readPostcondition: null,
                    hasActionPayload: false);
            }

            if (attemptResolution
                is AttemptResolution.TerminalOrPublishing terminal)
            {
                return await ReadTerminalOutcomeAsync(
                    executionId,
                    terminal.Execution.CurrentReference,
                    CancellationToken.None);
            }

            using var openAttempt =
                attemptResolution as AttemptResolution.Open;
            var stored = attemptResolution switch
            {
                AttemptResolution.Open open => open.Execution,
                AttemptResolution.DeadlineExceeded deadline =>
                    deadline.Execution,
                _ => throw new InvalidOperationException(
                    "Refresh attempt boundary returned an unsupported resolution."),
            };
            var checkpoint = await checkpointStore.ReadAsync(
                executionId,
                CancellationToken.None);
            TerminalCandidate terminalCandidate;
            if (attemptResolution
                is AttemptResolution.DeadlineExceeded)
            {
                terminalCandidate = CreateDeadlineCandidate(
                    stored.Start,
                    checkpoint,
                    canAttributeCurrentProviderObservation: true);
            }
            else
            {
                var completion = await attemptBoundary.ObserveCompletionAsync(
                    Kind,
                    openAttempt,
                    ExecuteAsync(
                        executionId,
                        checkpoint,
                        claimantEndpointRegistrationGenerationId,
                        openAttempt.DeadlineCancellationToken,
                        enterRecoveryWhenReconnecting: false));
                if (completion
                    is AttemptResolution.Completed<TerminalCandidate>
                        completed)
                {
                    terminalCandidate = completed.Result;
                }
                else
                {
                    if (completion is AttemptResolution.Missing)
                    {
                        throw new InvalidOperationException(
                            "Refresh execution disappeared before deadline classification.");
                    }
                    if (completion
                        is AttemptResolution.TerminalOrPublishing
                            cancellationTerminal)
                    {
                        return await ReadTerminalOutcomeAsync(
                            executionId,
                            cancellationTerminal.Execution.CurrentReference,
                            CancellationToken.None);
                    }
                    if (completion
                        is not AttemptResolution.DeadlineExceeded deadline)
                    {
                        throw new InvalidOperationException(
                            "Refresh cancellation returned an unsupported deadline resolution.");
                    }

                    stored = deadline.Execution;
                    checkpoint = await checkpointStore.ReadAsync(
                        executionId,
                        CancellationToken.None);
                    terminalCandidate = CreateDeadlineCandidate(
                        stored.Start,
                        checkpoint,
                        canAttributeCurrentProviderObservation: true);
                }
            }
            if (terminalCandidate == null)
            {
                return await ReadTerminalOutcomeAsync(
                    executionId,
                    stored.CurrentReference,
                    CancellationToken.None);
            }

            terminalCandidate = ResolveTerminalCandidate(
                stored.Start,
                terminalCandidate);
            return await PublishAndCreateResponseAsync(
                terminalCandidate,
                stored.CurrentReference,
                CancellationToken.None);
        }

        public async ValueTask RecoverAsync (
            LifecycleExecutionRecoveryRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var claimantEndpointRegistrationGenerationId =
                request.Start.Host
                    .CurrentEndpointRegistrationGenerationId;
            var executionId = request.Start.LifecycleExecutionRef.Id;
            var attemptResolution = await attemptBoundary.ResolveRecoveryAsync(
                Kind,
                executionId,
                cancellationToken);
            if (attemptResolution is AttemptResolution.Missing)
            {
                return;
            }
            if (attemptResolution
                is AttemptResolution.TerminalOrPublishing terminal)
            {
                await terminalPublication.TryRecoverDuringRecoveryAsync(
                    executionId,
                    terminal.Execution.CurrentReference,
                    cancellationToken);
                return;
            }

            using var openAttempt =
                attemptResolution as AttemptResolution.Open;
            var execution = attemptResolution switch
            {
                AttemptResolution.Open open => open.Execution,
                AttemptResolution.DeadlineExceeded deadline =>
                    deadline.Execution,
                _ => throw new InvalidOperationException(
                    "Refresh recovery attempt boundary returned an unsupported resolution."),
            };
            var checkpoint = await checkpointStore.ReadAsync(
                executionId,
                cancellationToken);
            if (request.RejectionReason.HasValue)
            {
                var rejectionCandidate = CreateRecoveryRejectionCandidate(
                    request.Start,
                    request.RejectionReason.Value,
                    checkpoint,
                    request.CanAttributeCurrentProviderObservation);
                rejectionCandidate = ResolveTerminalCandidate(
                    request.Start,
                    rejectionCandidate);
                await TryPublishDuringRecoveryAsync(
                    rejectionCandidate,
                    execution.CurrentReference,
                    cancellationToken);
                return;
            }
            if (checkpoint == null)
            {
                if (attemptResolution
                    is AttemptResolution.DeadlineExceeded)
                {
                    var deadlineCandidate = CreateDeadlineCandidate(
                        execution.Start,
                        checkpoint: null,
                        canAttributeCurrentProviderObservation:
                            request.CanAttributeCurrentProviderObservation);
                    deadlineCandidate = ResolveTerminalCandidate(
                        execution.Start,
                        deadlineCandidate);
                    await TryPublishDuringRecoveryAsync(
                        deadlineCandidate,
                        execution.CurrentReference,
                        CancellationToken.None);
                }

                return;
            }

            TerminalCandidate terminalCandidate;
            if (attemptResolution
                is AttemptResolution.DeadlineExceeded)
            {
                terminalCandidate = CreateDeadlineCandidate(
                    execution.Start,
                    checkpoint,
                    request.CanAttributeCurrentProviderObservation);
            }
            else
            {
                var completion = await attemptBoundary.ObserveCompletionAsync(
                    Kind,
                    openAttempt,
                    ExecuteAsync(
                        executionId,
                        checkpoint,
                        claimantEndpointRegistrationGenerationId,
                        openAttempt.DeadlineCancellationToken,
                        enterRecoveryWhenReconnecting: true));
                if (completion
                    is AttemptResolution.Completed<TerminalCandidate>
                        completed)
                {
                    terminalCandidate = completed.Result;
                }
                else
                {
                    if (completion is AttemptResolution.Missing)
                    {
                        return;
                    }
                    if (completion
                        is AttemptResolution.TerminalOrPublishing
                            cancellationTerminal)
                    {
                        await terminalPublication.TryRecoverDuringRecoveryAsync(
                            executionId,
                            cancellationTerminal.Execution.CurrentReference,
                            CancellationToken.None);
                        return;
                    }
                    if (completion
                        is not AttemptResolution.DeadlineExceeded deadline)
                    {
                        throw new InvalidOperationException(
                            "Refresh recovery cancellation returned an unsupported deadline resolution.");
                    }

                    execution = deadline.Execution;
                    checkpoint = await checkpointStore.ReadAsync(
                        executionId,
                        CancellationToken.None);
                    terminalCandidate = CreateDeadlineCandidate(
                        execution.Start,
                        checkpoint,
                        request.CanAttributeCurrentProviderObservation);
                }
            }
            if (terminalCandidate == null)
            {
                await terminalPublication.TryRecoverDuringRecoveryAsync(
                    executionId,
                    execution.CurrentReference,
                    cancellationToken);
                return;
            }

            terminalCandidate = ResolveTerminalCandidate(
                request.Start,
                terminalCandidate);
            await TryPublishDuringRecoveryAsync(
                terminalCandidate,
                execution.CurrentReference,
                CancellationToken.None);
        }

        private async Task<TerminalCandidate> ExecuteAsync (
            Guid executionId,
            RefreshLifecycleExecutionCheckpoint checkpoint,
            Guid claimantEndpointRegistrationGenerationId,
            CancellationToken executionCancellationToken,
            bool enterRecoveryWhenReconnecting)
        {
            if (checkpoint == null || !checkpoint.SideEffectAdmitted)
            {
                if (checkpoint == null)
                {
                    var beforeSnapshot = provider.CaptureObservation();
                    checkpoint = await checkpointStore.WritePreparedAsync(
                        executionId,
                        CreateObservation(beforeSnapshot),
                        executionCancellationToken);
                }

                var admission =
                    await ResolveSideEffectAdmissionAsync(
                        checkpoint,
                        claimantEndpointRegistrationGenerationId,
                        executionCancellationToken);
                if (admission.State
                    == LifecycleExecutionSideEffectAdmissionCoordinator.Outcome
                        .Terminal)
                {
                    return null;
                }

                checkpoint = admission.Checkpoint;
                if (admission.State
                    == LifecycleExecutionSideEffectAdmissionCoordinator.Outcome
                        .Recover)
                {
                    if (enterRecoveryWhenReconnecting
                        && !await TryEnterRecoveryAfterAdmissionAsync(
                            executionId,
                            executionCancellationToken))
                    {
                        return null;
                    }

                    var nonOwnerSnapshot =
                        await WaitUntilRecoveredRefreshSettledAsync(
                            provider,
                            checkpoint,
                            executionCancellationToken);
                    return CreateCompletedCandidate(
                        executionId,
                        checkpoint,
                        nonOwnerSnapshot);
                }

                var mutationActivity = provider.BeginMutation();
                Task<UnityEditorRuntimeObservation> settleTask = null;
                try
                {
                    settleTask = WaitUntilRefreshSettledAsync(
                        provider,
                        CancellationToken.None);
                    _ = settleTask.ContinueWith(
                        static (completedTask, state) =>
                        {
                            _ = completedTask.Exception;
                            if (completedTask.Status == TaskStatus.RanToCompletion)
                            {
                                ((IUnityMutationActivity)state).Complete();
                            }
                        },
                        mutationActivity,
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    try
                    {
                        executionCancellationToken
                            .ThrowIfCancellationRequested();
                        checkpoint = await checkpointStore
                            .MarkDispatchPreparedAsync(
                                checkpoint,
                                new RefreshLifecycleDispatchCandidate(
                                    DateTimeOffset.UtcNow,
                                    checkpoint.Before.State.Generations
                                        .DomainReloadGeneration),
                                CancellationToken.None);
                        executionCancellationToken
                            .ThrowIfCancellationRequested();
                        provider.RequestRefresh();
                    }
                    catch (OperationCanceledException) when (
                        executionCancellationToken.IsCancellationRequested)
                    {
                        mutationActivity.Complete();
                        throw;
                    }
                    catch (UnityAssetRefreshException)
                    {
                        checkpoint = await checkpointStore
                            .MarkProviderInvocationObservedAsync(
                                checkpoint,
                                CancellationToken.None);
                        return CreateActionFailureCandidate(
                            executionId,
                            checkpoint);
                    }
                    checkpoint = await checkpointStore.MarkProviderReturnedAsync(
                        checkpoint,
                        CancellationToken.None);
                    var afterSnapshot = await AwaitWithCancellationAsync(
                        settleTask,
                        executionCancellationToken);
                    return CreateCompletedCandidate(
                        executionId,
                        checkpoint,
                        afterSnapshot);
                }
                finally
                {
                    if (settleTask == null)
                    {
                        mutationActivity.Complete();
                    }
                }
            }

            if (enterRecoveryWhenReconnecting
                && !await TryEnterRecoveryAfterAdmissionAsync(
                    executionId,
                    executionCancellationToken))
            {
                return null;
            }

            var recoveredSnapshot = await WaitUntilRecoveredRefreshSettledAsync(
                provider,
                checkpoint,
                executionCancellationToken);
            return CreateCompletedCandidate(
                executionId,
                checkpoint,
                recoveredSnapshot);
        }

        private async ValueTask<bool>
            TryEnterRecoveryAfterAdmissionAsync (
            Guid executionId,
            CancellationToken cancellationToken)
        {
            var outcome = await executionStore.TryEnterRecoveryAsync(
                Kind,
                executionId,
                cancellationToken);
            return outcome switch
            {
                LifecycleExecutionRecoveryTransitionOutcome.Entered => true,
                LifecycleExecutionRecoveryTransitionOutcome
                    .AlreadyRecovering => true,
                LifecycleExecutionRecoveryTransitionOutcome
                    .TerminalOrPublishing => false,
                LifecycleExecutionRecoveryTransitionOutcome
                    .SideEffectAdmissionRequired =>
                    throw new InvalidOperationException(
                        "Refresh recovery cannot precede its side-effect admission."),
                LifecycleExecutionRecoveryTransitionOutcome.Missing =>
                    throw new InvalidOperationException(
                        "Refresh execution disappeared while entering recovery."),
                _ => throw new InvalidOperationException(
                    $"Refresh recovery transition could not classify outcome '{outcome}'."),
            };
        }

        private async ValueTask<
                LifecycleExecutionSideEffectAdmissionCoordinator.Resolution<
                    RefreshLifecycleExecutionCheckpoint>>
            ResolveSideEffectAdmissionAsync (
            RefreshLifecycleExecutionCheckpoint checkpoint,
            Guid claimantEndpointRegistrationGenerationId,
            CancellationToken cancellationToken)
        {
            var stored = await executionStore.ReadAsync(
                Kind,
                checkpoint.ExecutionId,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "Refresh execution disappeared before side-effect admission.");
            if (stored.IsTerminal || stored.IsPublishing)
            {
                return await sideEffectAdmission.ReconnectAsync(
                    Kind,
                    stored,
                    claimantEndpointRegistrationGenerationId,
                    checkpointStore,
                    checkpoint,
                    cancellationToken);
            }
            if (IsActionState(
                    stored.CurrentReference,
                    LifecycleExecutionState.Registered))
            {
                if (checkpoint.SideEffectAdmitted)
                {
                    throw new InvalidOperationException(
                        "Refresh side-effect admission marker cannot precede its durable execution right.");
                }

                var refreshingReference =
                    LifecycleExecutionReferenceFactory.CreateStateProjection(
                        stored.CurrentReference,
                        ExecutionLifecycle.Active,
                        LifecycleExecutionState.Refreshing);
                var acquired = await sideEffectAdmission.AcquireAsync(
                    Kind,
                    stored,
                    refreshingReference,
                    claimantEndpointRegistrationGenerationId,
                    checkpointStore,
                    checkpoint,
                    cancellationToken);
                EnsureRecoverableAdmission(acquired);
                return acquired;
            }
            if (!CanRecoverWithoutDispatch(stored.CurrentReference))
            {
                throw new InvalidOperationException(
                    $"Refresh execution state '{stored.CurrentReference.State.Value}' cannot admit or recover its side effect.");
            }

            var resolution = await sideEffectAdmission.ReconnectAsync(
                Kind,
                stored,
                claimantEndpointRegistrationGenerationId,
                checkpointStore,
                checkpoint,
                cancellationToken);
            EnsureRecoverableAdmission(resolution);
            return resolution;
        }

        private static void EnsureRecoverableAdmission (
            LifecycleExecutionSideEffectAdmissionCoordinator.Resolution<
                RefreshLifecycleExecutionCheckpoint> resolution)
        {
            if (resolution.State
                    == LifecycleExecutionSideEffectAdmissionCoordinator.Outcome
                        .Recover
                && !CanRecoverWithoutDispatch(
                    resolution.AuthoritativeExecution.CurrentReference))
            {
                throw new InvalidOperationException(
                    $"Refresh execution state '{resolution.AuthoritativeExecution.CurrentReference.State.Value}' cannot recover its side effect.");
            }
        }

        private async ValueTask<RefreshLifecycleExecutionOutcome>
            PublishAndCreateResponseAsync (
            TerminalCandidate terminalCandidate,
            ExecutionRef authoritativeReconnectableReference,
            CancellationToken cancellationToken)
        {
            var publication = await terminalPublication.PublishAsync(
                terminalCandidate.ExecutionId,
                authoritativeReconnectableReference,
                start => CreateTerminalRecord(start, terminalCandidate),
                cancellationToken);
            if (publication
                is TerminalPublication.PublicationFailed publicationFailed)
            {
                return await CreateTerminalPublicationFailureOutcomeAsync(
                    publicationFailed.TerminalRecord,
                    publicationFailed.ReconnectableReference);
            }
            if (publication is TerminalPublication.Verified verified)
            {
                var checkpoint =
                    await TryReadCheckpointForTerminalProjectionAsync(
                        terminalCandidate.ExecutionId);
                return CreateTerminalOutcome(
                    verified.TerminalReference,
                    verified.TerminalRecord,
                    checkpoint);
            }

            var unavailable = (TerminalPublication.Unavailable)publication;
            var unavailableCheckpoint =
                await TryReadCheckpointForTerminalProjectionAsync(
                    terminalCandidate.ExecutionId);
            var refresh = ResolveRefreshEvidence(
                terminalCandidate,
                unavailableCheckpoint);
            return CreateErrorResponse(
                LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                TerminalPublicationFailureMessage,
                unavailable.ReconnectableReference,
                terminalCandidate.ApplicationState,
                terminalCandidate.Result,
                refresh,
                observedLifecycle: terminalCandidate.Result?.Lifecycle,
                readPostcondition:
                    terminalCandidate.Result?.ReadPostcondition
                    ?? CreateReadPostconditionWhenApplied(
                        refresh,
                        terminalCandidate.ApplicationState));
        }

        private async ValueTask<RefreshLifecycleExecutionOutcome>
            ReadTerminalOutcomeAsync (
            Guid executionId,
            ExecutionRef authoritativeExecutionReference,
            CancellationToken cancellationToken)
        {
            var publication = await terminalPublication.RecoverAsync(
                    executionId,
                    authoritativeExecutionReference,
                    cancellationToken);
            if (publication
                is TerminalPublication.PublicationFailed publicationFailed)
            {
                return await CreateTerminalPublicationFailureOutcomeAsync(
                    publicationFailed.TerminalRecord,
                    publicationFailed.ReconnectableReference);
            }
            if (publication is TerminalPublication.Verified verified)
            {
                var checkpoint =
                    await TryReadCheckpointForTerminalProjectionAsync(
                        executionId);
                return CreateTerminalOutcome(
                    verified.TerminalReference,
                    verified.TerminalRecord,
                    checkpoint);
            }

            var unavailable = (TerminalPublication.Unavailable)publication;
            return CreateErrorResponse(
                LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                TerminalPublicationFailureMessage,
                unavailable.ReconnectableReference,
                ExecutionApplicationState.Unknown,
                result: null,
                refresh: null,
                observedLifecycle: null,
                readPostcondition: null);
        }

        private RefreshLifecycleExecutionOutcome CreateTerminalOutcome (
            TerminalExecutionRef terminalReference,
            RefreshLifecycleExecutionTerminalRecord terminalRecord,
            RefreshLifecycleExecutionCheckpoint checkpoint)
        {
            if (terminalRecord.TerminalReason
                    == LifecycleExecutionTerminalReason.Completed
                && terminalRecord.Result != null)
            {
                return RefreshLifecycleExecutionOutcome.Completed(
                    terminalRecord.Project,
                    terminalReference,
                    terminalRecord.Result);
            }

            var error = CreateRecoveryError(terminalRecord.TerminalReason);
            var refresh = ResolveRefreshEvidence(terminalRecord, checkpoint);
            return RefreshLifecycleExecutionOutcome.Failed(
                terminalRecord.Project,
                error.Code,
                error.Message,
                terminalReference,
                terminalRecord.ApplicationState,
                terminalRecord.Result,
                refresh,
                terminalRecord.Result?.Lifecycle,
                terminalRecord.Result?.ReadPostcondition
                    ?? CreateReadPostconditionWhenApplied(
                        refresh,
                        terminalRecord.ApplicationState),
                error.InstancePath);
        }

        private async ValueTask TryPublishDuringRecoveryAsync (
            TerminalCandidate terminalCandidate,
            ExecutionRef authoritativeReconnectableReference,
            CancellationToken cancellationToken)
        {
            await terminalPublication.TryPublishDuringRecoveryAsync(
                terminalCandidate.ExecutionId,
                authoritativeReconnectableReference,
                start => CreateTerminalRecord(start, terminalCandidate),
                cancellationToken);
        }

        private static RefreshLifecycleExecutionTerminalRecord
            CreateTerminalRecord (
            LifecycleExecutionStartBinding start,
            TerminalCandidate terminalCandidate)
        {
            var executionId = terminalCandidate.ExecutionId;
            return new RefreshLifecycleExecutionTerminalRecord(
                executionId,
                start.LifecycleExecutionRef.DefinitionDigest,
                start.Project,
                start.Host,
                start.StartedGeneration,
                terminalCandidate.TerminalGeneration,
                start.DeadlineUtc,
                start.StartedAtUtc,
                terminalCandidate.CompletedAtUtc,
                terminalCandidate.TerminalReason,
                terminalCandidate.ApplicationState,
                terminalCandidate.Result,
                verdict: null,
                Array.Empty<ArtifactRef>());
        }

        private async ValueTask<RefreshLifecycleExecutionOutcome>
            CreateTerminalPublicationFailureOutcomeAsync (
            RefreshLifecycleExecutionTerminalRecord fixedTerminalRecord,
            ExecutionRef reconnectableReference)
        {
            var checkpoint = await TryReadCheckpointForTerminalProjectionAsync(
                fixedTerminalRecord.ExecutionId);
            var refresh = ResolveRefreshEvidence(
                fixedTerminalRecord,
                checkpoint);
            return RefreshLifecycleExecutionOutcome.Failed(
                fixedTerminalRecord.Project,
                LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                TerminalPublicationFailureMessage,
                reconnectableReference,
                fixedTerminalRecord.ApplicationState,
                fixedTerminalRecord.Result,
                refresh,
                fixedTerminalRecord.Result?.Lifecycle,
                fixedTerminalRecord.Result?.ReadPostcondition
                    ?? CreateReadPostconditionWhenApplied(
                        refresh,
                        fixedTerminalRecord.ApplicationState));
        }

        private async ValueTask<RefreshLifecycleExecutionCheckpoint>
            TryReadCheckpointForTerminalProjectionAsync (Guid executionId)
        {
            try
            {
                return await checkpointStore.ReadAsync(
                    executionId,
                    CancellationToken.None);
            }
            catch (IOException exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "Refresh action evidence could not be read after its terminal record was fixed.",
                    exception);
                return null;
            }
        }

        private TerminalCandidate CreateCompletedCandidate (
            Guid executionId,
            RefreshLifecycleExecutionCheckpoint checkpoint,
            UnityEditorRuntimeObservation afterSnapshot)
        {
            var completedAtUtc = DateTimeOffset.UtcNow;
            var lifecycle = CreateObservation(afterSnapshot);
            var result = new RefreshLifecycleResult(
                new RefreshLifecycleResult.RefreshEvidence(
                    checkpoint.DispatchCandidate.StartedAtUtc,
                    completedAtUtc,
                    checkpoint.DispatchCandidate.DomainReloadGenerationBefore
                        ?? checkpoint.Before.State.Generations.DomainReloadGeneration,
                    afterSnapshot.State.Generations.DomainReloadGeneration),
                lifecycle,
                CreateReadPostcondition(
                    checkpoint.DispatchCandidate.StartedAtUtc));
            return new TerminalCandidate(
                executionId,
                result,
                LifecycleExecutionTerminalReason.Completed,
                ExecutionApplicationState.Applied,
                afterSnapshot.State.Generations,
                completedAtUtc);
        }

        private TerminalCandidate CreateDeadlineCandidate (
            LifecycleExecutionStartBinding start,
            RefreshLifecycleExecutionCheckpoint checkpoint,
            bool canAttributeCurrentProviderObservation)
        {
            var snapshot = canAttributeCurrentProviderObservation
                ? provider.CaptureObservation()
                : null;
            var sideEffectAdmitted = checkpoint?.SideEffectAdmitted == true;
            var applicationState =
                checkpoint?.ProviderReturned == true
                    ? ExecutionApplicationState.Applied
                    : !sideEffectAdmitted
                    && IsActionState(
                        start.LifecycleExecutionRef,
                        LifecycleExecutionState.Registered)
                ? ExecutionApplicationState.NotApplied
                : ExecutionApplicationState.Indeterminate;
            return new TerminalCandidate(
                start.LifecycleExecutionRef.Id,
                Result: null,
                LifecycleExecutionTerminalReason.DeadlineExceeded,
                applicationState,
                snapshot?.State.Generations,
                DateTimeOffset.UtcNow);
        }

        private TerminalCandidate
            CreateActionFailureCandidate (
                Guid executionId,
                RefreshLifecycleExecutionCheckpoint checkpoint)
        {
            var snapshot = provider.CaptureObservation();
            var applicationState = checkpoint?.SideEffectAdmitted == true
                ? ExecutionApplicationState.Indeterminate
                : ExecutionApplicationState.NotApplied;
            return new TerminalCandidate(
                executionId,
                Result: null,
                LifecycleExecutionTerminalReason.ActionFailed,
                applicationState,
                snapshot.State.Generations,
                DateTimeOffset.UtcNow);
        }

        private TerminalCandidate
            CreateRecoveryRejectionCandidate (
                LifecycleExecutionStartBinding start,
                LifecycleExecutionTerminalReason reason,
                RefreshLifecycleExecutionCheckpoint checkpoint,
                bool canAttributeCurrentProviderObservation)
        {
            var canAttributeGeneration =
                canAttributeCurrentProviderObservation
                && LifecycleExecutionTerminalFactsPolicy
                    .CanAttributeObservedGeneration(reason);
            var snapshot = canAttributeGeneration
                ? provider.CaptureObservation()
                : null;
            var sideEffectAdmitted =
                checkpoint?.SideEffectAdmitted == true;
            var applicationState =
                LifecycleExecutionTerminalFactsPolicy
                    .ResolveUnprovenApplicationState(
                    start.LifecycleExecutionRef,
                    sideEffectAdmitted);
            return new TerminalCandidate(
                start.LifecycleExecutionRef.Id,
                Result: null,
                reason,
                applicationState,
                canAttributeGeneration
                    ? snapshot?.State.Generations
                    : null,
                DateTimeOffset.UtcNow);
        }

        private static TerminalCandidate
            ResolveTerminalCandidate (
                LifecycleExecutionStartBinding start,
                TerminalCandidate candidate)
        {
            var fixedAtUtc = DateTimeOffset.UtcNow;
            var terminalFacts =
                LifecycleExecutionTerminalFactsPolicy.ResolveTerminalFacts(
                    start,
                    candidate.TerminalReason,
                    candidate.ApplicationState,
                    candidate.TerminalGeneration,
                    fixedAtUtc >= start.DeadlineUtc
                        ? fixedAtUtc
                        : candidate.CompletedAtUtc);
            var generationWasRejected =
                candidate.TerminalGeneration != null
                && terminalFacts.TerminalGeneration == null
                && LifecycleExecutionTerminalFactsPolicy
                    .CanAttributeObservedGeneration(
                        candidate.TerminalReason);

            return new TerminalCandidate(
                candidate.ExecutionId,
                generationWasRejected
                    ? null
                    : candidate.Result,
                terminalFacts.TerminalReason,
                terminalFacts.ApplicationState,
                terminalFacts.TerminalGeneration,
                terminalFacts.CompletedAtUtc);
        }

        private static RefreshLifecycleExecutionError CreateRecoveryError (
            LifecycleExecutionTerminalReason reason)
        {
            return reason switch
            {
                LifecycleExecutionTerminalReason.DeadlineExceeded => new RefreshLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.DeadlineExceeded,
                    "Refresh reached its durable execution deadline.",
                    null),
                LifecycleExecutionTerminalReason.ProjectMismatch => new RefreshLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.ProjectMismatch,
                    "Refresh recovery project does not match its durable start.",
                    null),
                LifecycleExecutionTerminalReason.HostMismatch => new RefreshLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.HostMismatch,
                    "Refresh recovery host does not match its durable start.",
                    null),
                LifecycleExecutionTerminalReason.GenerationMismatch => new RefreshLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.GenerationMismatch,
                    "Refresh recovery generation was not a proven successor.",
                    null),
                LifecycleExecutionTerminalReason.UnityExited => new RefreshLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.UnityExited,
                    "The Unity Editor hosting refresh exited before completion.",
                    null),
                _ => new RefreshLifecycleExecutionError(
                    UcliCoreErrorCodes.InternalError,
                    "Refresh recovery ended with an explicit action failure.",
                    null),
            };
        }

        private static RefreshLifecycleStartEvidence ResolveRefreshEvidence (
            RefreshLifecycleExecutionTerminalRecord terminalRecord,
            RefreshLifecycleExecutionCheckpoint checkpoint)
        {
            return ResolveRefreshEvidence(
                terminalRecord.ApplicationState,
                terminalRecord.Result,
                checkpoint,
                terminalRecord.TerminalGeneration);
        }

        private static RefreshLifecycleStartEvidence ResolveRefreshEvidence (
            TerminalCandidate terminalCandidate,
            RefreshLifecycleExecutionCheckpoint checkpoint)
        {
            return ResolveRefreshEvidence(
                terminalCandidate.ApplicationState,
                terminalCandidate.Result,
                checkpoint,
                terminalCandidate.TerminalGeneration);
        }

        private static RefreshLifecycleStartEvidence ResolveRefreshEvidence (
            ExecutionApplicationState applicationState,
            RefreshLifecycleResult result,
            RefreshLifecycleExecutionCheckpoint checkpoint,
            UnityEditorGenerationSnapshot terminalGeneration)
        {
            if (applicationState == ExecutionApplicationState.NotApplied)
            {
                return null;
            }
            if (result != null)
            {
                return new RefreshLifecycleStartEvidence(
                    result.Refresh.StartedAtUtc,
                    result.Refresh.DomainReloadGenerationBefore);
            }
            return HasObservedProviderInvocation(
                    checkpoint,
                    terminalGeneration)
                ? new RefreshLifecycleStartEvidence(
                    checkpoint.DispatchCandidate.StartedAtUtc,
                    checkpoint.DispatchCandidate.DomainReloadGenerationBefore)
                : null;
        }

        private static bool HasObservedProviderInvocation (
            RefreshLifecycleExecutionCheckpoint checkpoint,
            UnityEditorGenerationSnapshot terminalGeneration)
        {
            if (checkpoint?.DispatchCandidate == null)
            {
                return false;
            }
            if (checkpoint.ProviderInvocationObserved)
            {
                return true;
            }

            var beforeGeneration = checkpoint.Before?.State?.Generations;
            return beforeGeneration != null
                && terminalGeneration != null
                && LifecycleExecutionGenerationRules.IsMonotonicSuccessor(
                    beforeGeneration,
                    terminalGeneration)
                && terminalGeneration.AssetRefreshGeneration
                    > beforeGeneration.AssetRefreshGeneration;
        }

        private UnityEditorObservation CreateObservation (
            UnityEditorRuntimeObservation snapshot)
        {
            return provider.CreateLifecycleObservation(snapshot);
        }

        private static ExecutionReadPostcondition CreateReadPostcondition (
            DateTimeOffset minSafeGeneratedAtUtc)
        {
            return new ExecutionReadPostcondition(new[]
            {
                new ExecutionReadPostconditionRequirement(
                    ExecutionReadPostconditionSurface.AssetSearch,
                    minSafeGeneratedAtUtc,
                    ScenePath: null),
                new ExecutionReadPostconditionRequirement(
                    ExecutionReadPostconditionSurface.GuidPath,
                    minSafeGeneratedAtUtc,
                    ScenePath: null),
                new ExecutionReadPostconditionRequirement(
                    ExecutionReadPostconditionSurface.SceneTreeLite,
                    minSafeGeneratedAtUtc,
                    ScenePath: null),
            });
        }

        private static ExecutionReadPostcondition
            CreateReadPostconditionWhenApplied (
                RefreshLifecycleStartEvidence refresh,
                ExecutionApplicationState applicationState)
        {
            return applicationState == ExecutionApplicationState.NotApplied
                || refresh == null
                    ? null
                    : CreateReadPostcondition(refresh.StartedAtUtc);
        }

        private static async Task<UnityEditorRuntimeObservation>
            WaitUntilRefreshSettledAsync (
                IRefreshLifecycleExecutionProvider provider,
                CancellationToken cancellationToken)
        {
            var observationWindow = new SettledLifecycleObservationWindow();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = provider.CaptureObservation();
                if (observationWindow.Observe(snapshot))
                {
                    return snapshot;
                }

                await provider.WaitForNextUpdateAsync(
                    cancellationToken);
            }
        }

        private static async Task<UnityEditorRuntimeObservation>
            WaitUntilRecoveredRefreshSettledAsync (
                IRefreshLifecycleExecutionProvider provider,
                RefreshLifecycleExecutionCheckpoint checkpoint,
                CancellationToken cancellationToken)
        {
            var startedGeneration = checkpoint.Before.State.Generations;
            var observationWindow = new SettledLifecycleObservationWindow();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = provider.CaptureObservation();
                if (observationWindow.Observe(snapshot)
                    && (checkpoint.ProviderReturned
                        || !LifecycleExecutionGenerationRules.IsMonotonicSuccessor(
                            startedGeneration,
                            snapshot.State.Generations)
                        || snapshot.State.Generations.AssetRefreshGeneration
                            > startedGeneration.AssetRefreshGeneration))
                {
                    return snapshot;
                }

                await provider.WaitForNextUpdateAsync(
                    cancellationToken);
            }
        }

        private static async Task<T> AwaitWithCancellationAsync<T> (
            Task<T> task,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                return await task;
            }

            var cancellationSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(
                       static state =>
                           ((TaskCompletionSource<bool>)state).TrySetResult(true),
                       cancellationSource))
            {
                if (!ReferenceEquals(
                        await Task.WhenAny(task, cancellationSource.Task),
                        task))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return await task;
        }

        private static bool IsLifecycleSettled (
            UnityEditorRuntimeObservation snapshot)
        {
            return snapshot.State.LifecycleState
                is not UnityEditorLifecycleState.DomainReloading
                and not UnityEditorLifecycleState.Compiling
                and not UnityEditorLifecycleState.Reimporting
                and not UnityEditorLifecycleState.Recovering
                and not UnityEditorLifecycleState.Starting;
        }

        private static bool IsActionState (
            ExecutionRef executionReference,
            LifecycleExecutionState state)
        {
            return executionReference.Lifecycle == ExecutionLifecycle.Active
                && string.Equals(
                    executionReference.State.Value,
                    TextVocabulary.GetText(state),
                    StringComparison.Ordinal);
        }

        private static bool CanRecoverWithoutDispatch (
            ExecutionRef executionReference)
        {
            return IsActionState(
                    executionReference,
                    LifecycleExecutionState.Refreshing)
                || executionReference.Lifecycle == ExecutionLifecycle.Recovery
                    && (string.Equals(
                            executionReference.State.Value,
                            TextVocabulary.GetText(
                                LifecycleExecutionState.Recovering),
                            StringComparison.Ordinal)
                        || string.Equals(
                            executionReference.State.Value,
                            TextVocabulary.GetText(
                                LifecycleExecutionState.Publishing),
                            StringComparison.Ordinal));
        }

        private RefreshLifecycleExecutionOutcome CreateErrorResponse (
            UcliCode code,
            string message,
            ExecutionRef lifecycleExecutionRef,
            ExecutionApplicationState applicationState,
            RefreshLifecycleResult result,
            RefreshLifecycleStartEvidence refresh,
            UnityEditorObservation observedLifecycle,
            ExecutionReadPostcondition readPostcondition,
            string instancePath = null,
            bool hasActionPayload = true)
        {
            return RefreshLifecycleExecutionOutcome.Failed(
                provider.Project,
                code,
                message,
                lifecycleExecutionRef,
                applicationState,
                result,
                refresh,
                observedLifecycle,
                readPostcondition,
                instancePath,
                hasActionPayload);
        }

        private sealed record TerminalCandidate (
            Guid ExecutionId,
            RefreshLifecycleResult Result,
            LifecycleExecutionTerminalReason TerminalReason,
            ExecutionApplicationState ApplicationState,
            UnityEditorGenerationSnapshot TerminalGeneration,
            DateTimeOffset CompletedAtUtc);

        private sealed class SettledLifecycleObservationWindow
        {
            private int stableUpdates;
            private bool hasStableSnapshot;
            private UnityEditorLifecycleState stableLifecycleState;
            private UnityEditorCompileState stableCompileState;
            private UnityEditorGenerationSnapshot stableGenerations;

            public bool Observe (UnityEditorRuntimeObservation snapshot)
            {
                if (!IsLifecycleSettled(snapshot))
                {
                    Reset();
                    return false;
                }

                if (!hasStableSnapshot || !MatchesStableSnapshot(snapshot))
                {
                    stableUpdates = 0;
                    CaptureStableSnapshot(snapshot);
                }

                stableUpdates++;
                return stableUpdates >= RequiredStableLifecycleObservations;
            }

            private bool MatchesStableSnapshot (UnityEditorRuntimeObservation snapshot)
            {
                return stableLifecycleState == snapshot.State.LifecycleState
                    && stableCompileState == snapshot.State.CompileState
                    && stableGenerations == snapshot.State.Generations;
            }

            private void CaptureStableSnapshot (UnityEditorRuntimeObservation snapshot)
            {
                hasStableSnapshot = true;
                stableLifecycleState = snapshot.State.LifecycleState;
                stableCompileState = snapshot.State.CompileState;
                stableGenerations = snapshot.State.Generations;
            }

            private void Reset ()
            {
                stableUpdates = 0;
                hasStableSnapshot = false;
                stableLifecycleState = default;
                stableCompileState = default;
                stableGenerations = null;
            }
        }
    }
}
