using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Runtime;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using AttemptResolution =
    MackySoft.Ucli.Unity.Ipc.LifecycleExecutionAttemptResolution;
using TerminalPublication =
    MackySoft.Ucli.Unity.Ipc.LifecycleExecutionTerminalPublication<
        MackySoft.Ucli.Contracts.Execution.Lifecycle.PlayExitLifecycleExecutionTerminalRecord>;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Owns the typed <c>play.exit</c> Lifecycle Execution state machine and terminal publication.
    /// </summary>
    internal sealed class PlayExitLifecycleExecutionHandler :
        IPlayExitLifecycleExecutionHandler,
        ILifecycleExecutionRecoveryHandler
    {
        private const string TerminalPublicationFailureMessage =
            "Play Mode exit terminal record could not be published and reverified.";

        private readonly IPlayExitLifecycleExecutionProvider provider;
        private readonly FileLifecycleExecutionStore executionStore;
        private readonly LifecycleExecutionAttemptBoundary attemptBoundary;
        private readonly FilePlayExitLifecycleExecutionCheckpointStore checkpointStore;
        private readonly LifecycleExecutionSideEffectAdmissionCoordinator
            sideEffectAdmission;
        private readonly LifecycleExecutionTerminalPublicationBoundary<
            PlayExitLifecycleExecutionTerminalRecord>
            terminalPublication;

        public PlayExitLifecycleExecutionHandler (
            IPlayExitLifecycleExecutionProvider provider,
            FileLifecycleExecutionStore executionStore,
            FilePlayExitLifecycleExecutionCheckpointStore checkpointStore,
            IDaemonLogger daemonLogger)
        {
            this.provider = provider
                ?? throw new ArgumentNullException(nameof(provider));
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
                    PlayExitLifecycleExecutionTerminalRecord>(
                    Kind,
                    this.executionStore,
                    daemonLogger
                        ?? throw new ArgumentNullException(nameof(daemonLogger)),
                    "Play Mode exit terminal publication failed.",
                    "Play Mode exit terminal publication failed during recovery.");
        }

        /// <inheritdoc />
        public LifecycleExecutionKind Kind => LifecycleExecutionKind.PlayExit;

        /// <inheritdoc />
        public async ValueTask<PlayExitLifecycleExecutionOutcome> ExecuteAsync (
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
                return PlayExitLifecycleExecutionOutcome.Failed(
                    UcliCoreErrorCodes.InvalidArgument,
                    "Play Mode exit Lifecycle Execution was not registered.",
                    lifecycleExecutionRef: null,
                    ExecutionApplicationState.NotApplied,
                    result: null,
                    hasActionPayload: false);
            }

            if (attemptResolution
                is AttemptResolution.BindingMismatch bindingMismatch)
            {
                return PlayExitLifecycleExecutionOutcome.Failed(
                    LifecycleExecutionStartBindingMatcher
                        .GetMismatchErrorCode(bindingMismatch.Match),
                    "Play Mode exit request does not match its durable start binding.",
                    lifecycleExecutionRef: null,
                    ExecutionApplicationState.NotApplied,
                    result: null,
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
            var execution = attemptResolution switch
            {
                AttemptResolution.Open open => open.Execution,
                AttemptResolution.DeadlineExceeded deadline =>
                    deadline.Execution,
                _ => throw new InvalidOperationException(
                    "Play Mode exit attempt boundary returned an unsupported resolution."),
            };
            var checkpoint = await checkpointStore.ReadAsync(
                executionId,
                CancellationToken.None);
            if (attemptResolution
                is AttemptResolution.DeadlineExceeded)
            {
                return await FinalizeDeadlineAndCreateOutcomeAsync(
                    executionId,
                    execution,
                    checkpoint);
            }

            var completion = await attemptBoundary.ObserveCompletionAsync(
                Kind,
                openAttempt,
                ExecuteOpenAsync(
                    execution,
                    checkpoint,
                    claimantEndpointRegistrationGenerationId,
                    openAttempt.DeadlineCancellationToken).AsTask());
            if (completion
                is AttemptResolution.Completed<
                    PlayExitLifecycleExecutionOutcome> completed)
            {
                return completed.Result;
            }
            if (completion is AttemptResolution.Missing)
            {
                throw new InvalidOperationException(
                    "Play Mode exit execution disappeared before deadline classification.");
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
                is not AttemptResolution.DeadlineExceeded deadlineCancellation)
            {
                throw new InvalidOperationException(
                    "Play Mode exit cancellation returned an unsupported deadline resolution.");
            }

            checkpoint = await checkpointStore.ReadAsync(
                executionId,
                CancellationToken.None);
            return await FinalizeDeadlineAndCreateOutcomeAsync(
                executionId,
                deadlineCancellation.Execution,
                checkpoint);
        }

        /// <inheritdoc />
        public async ValueTask RecoverAsync (
            LifecycleExecutionRecoveryRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
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
                    "Play Mode exit recovery attempt boundary returned an unsupported resolution."),
            };
            var checkpoint = await checkpointStore.ReadAsync(
                executionId,
                cancellationToken);
            if (request.RejectionReason.HasValue)
            {
                if (checkpoint == null
                    && request.CanAttributeCurrentProviderObservation)
                {
                    checkpoint = await EnsureCheckpointAsync(
                        executionId,
                        provider.CaptureObservation());
                }

                await FinalizeWithoutResponseAsync(
                    executionId,
                    CreateSchedulerRejectionCandidate(
                        execution.Start,
                        request.RejectionReason.Value,
                        GetSchedulerApplicationState(
                            execution,
                            checkpoint),
                        request.CanAttributeCurrentProviderObservation),
                    execution.CurrentReference,
                    cancellationToken);
                return;
            }

            // A durable start without an action checkpoint means no Play Mode exit request reached
            // the action owner, so bootstrap recovery has no side effect or result to recover.
            if (checkpoint == null)
            {
                return;
            }

            if (attemptResolution
                is AttemptResolution.DeadlineExceeded)
            {
                await FinalizeDeadlineWithoutResponseAsync(
                    executionId,
                    execution,
                    checkpoint,
                    request.CanAttributeCurrentProviderObservation);
                return;
            }

            var completion = await attemptBoundary.ObserveCompletionAsync(
                Kind,
                openAttempt,
                ContinueRecoveryOpenAsync(
                    execution,
                    checkpoint,
                    claimantEndpointRegistrationGenerationId,
                    openAttempt.DeadlineCancellationToken,
                    cancellationToken));
            if (completion is AttemptResolution.Completed)
            {
                return;
            }
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
                is not AttemptResolution.DeadlineExceeded deadlineCancellation)
            {
                throw new InvalidOperationException(
                    "Play Mode exit recovery cancellation returned an unsupported deadline resolution.");
            }

            checkpoint = await checkpointStore.ReadAsync(
                executionId,
                CancellationToken.None);
            await FinalizeDeadlineWithoutResponseAsync(
                executionId,
                deadlineCancellation.Execution,
                checkpoint,
                request.CanAttributeCurrentProviderObservation);
        }

        private async Task ContinueRecoveryOpenAsync (
            StoredLifecycleExecution execution,
            PlayExitLifecycleExecutionCheckpoint checkpoint,
            Guid claimantEndpointRegistrationGenerationId,
            CancellationToken executionDeadlineCancellationToken,
            CancellationToken recoveryCancellationToken)
        {
            var executionId = execution.Start.LifecycleExecutionRef.Id;
            using var admissionCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    executionDeadlineCancellationToken,
                    recoveryCancellationToken);
            var continuation = await AcquireContinuationAsync(
                executionId,
                claimantEndpointRegistrationGenerationId,
                admissionCancellation.Token);
            PlayExitTransitionExecutionResult result;
            switch (continuation)
            {
                case Continuation.Issue:
                    result = await provider.IssueAsync(
                        checkpoint.Before,
                        executionDeadlineCancellationToken);
                    break;
                case Continuation.Recover:
                    result = await provider.RecoverAsync(
                        checkpoint.Before,
                        executionDeadlineCancellationToken);
                    break;
                case Continuation.Terminal:
                    await terminalPublication.TryRecoverDuringRecoveryAsync(
                        executionId,
                        execution.CurrentReference,
                        recoveryCancellationToken);
                    return;
                default:
                    throw new InvalidOperationException(
                        "Play Mode exit recovery did not resolve a continuation.");
            }

            var current = await RequireOpenExecutionAsync(
                executionId,
                recoveryCancellationToken);
            await FinalizeWithoutResponseAsync(
                executionId,
                CreateTerminalCandidate(current.Start, result),
                current.CurrentReference,
                recoveryCancellationToken);
        }

        private async ValueTask<PlayExitLifecycleExecutionOutcome>
            ExecuteOpenAsync (
            StoredLifecycleExecution execution,
            PlayExitLifecycleExecutionCheckpoint checkpoint,
            Guid claimantEndpointRegistrationGenerationId,
            CancellationToken executionDeadlineCancellationToken)
        {
            var executionId = execution.Start.LifecycleExecutionRef.Id;
            if (checkpoint == null)
            {
                var preparation = provider.Prepare(
                    executionDeadlineCancellationToken);
                if (!preparation.RequiresSideEffect)
                {
                    await EnsureCheckpointAsync(
                        executionId,
                        preparation.TerminalResult.Result.Before);
                    return await FinalizeAndCreateOutcomeAsync(
                        executionId,
                        CreateTerminalCandidate(
                            execution.Start,
                            preparation.TerminalResult),
                        execution.CurrentReference);
                }

                checkpoint = await EnsureCheckpointAsync(
                    executionId,
                    preparation.Before);
            }

            var continuation = await AcquireContinuationAsync(
                executionId,
                claimantEndpointRegistrationGenerationId,
                executionDeadlineCancellationToken);
            if (continuation == Continuation.Terminal)
            {
                return await ReadTerminalOutcomeAsync(
                    executionId,
                    execution.CurrentReference,
                    CancellationToken.None);
            }

            var result = continuation == Continuation.Issue
                ? await provider.IssueAsync(
                    checkpoint.Before,
                    executionDeadlineCancellationToken)
                : await provider.RecoverAsync(
                    checkpoint.Before,
                    executionDeadlineCancellationToken);
            var current = await RequireOpenExecutionAsync(
                executionId,
                CancellationToken.None);
            return await FinalizeAndCreateOutcomeAsync(
                executionId,
                CreateTerminalCandidate(current.Start, result),
                current.CurrentReference);
        }

        private async ValueTask<Continuation> AcquireContinuationAsync (
            Guid executionId,
            Guid claimantEndpointRegistrationGenerationId,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var execution = await executionStore.ReadAsync(
                    Kind,
                    executionId,
                    cancellationToken);
                if (execution == null)
                {
                    throw new InvalidOperationException(
                        "Play Mode exit Lifecycle Execution disappeared during continuation admission.");
                }
                if (execution.IsTerminal || execution.IsPublishing)
                {
                    return Continuation.Terminal;
                }

                var checkpoint = await checkpointStore.ReadAsync(
                    executionId,
                    cancellationToken);
                var currentReference = execution.CurrentReference;
                var state = GetState(currentReference);
                if (state == LifecycleExecutionState.Registered)
                {
                    if (checkpoint == null)
                    {
                        throw new InvalidOperationException(
                            "Play Mode exit side-effect admission requires its durable before observation.");
                    }
                    var exitingReference =
                        LifecycleExecutionReferenceFactory.CreateStateProjection(
                            currentReference,
                            ExecutionLifecycle.Active,
                            LifecycleExecutionState.Exiting);
                    LifecycleExecutionSideEffectAdmissionCoordinator.Resolution<
                        PlayExitLifecycleExecutionCheckpoint> admission;
                    admission = await sideEffectAdmission.AcquireAsync(
                        Kind,
                        execution,
                        exitingReference,
                        claimantEndpointRegistrationGenerationId,
                        checkpointStore,
                        checkpoint,
                        cancellationToken);
                    if (admission.State
                        == LifecycleExecutionSideEffectAdmissionCoordinator
                            .Outcome.Terminal)
                    {
                        return Continuation.Terminal;
                    }
                    if (admission.State
                        == LifecycleExecutionSideEffectAdmissionCoordinator
                            .Outcome.Acquired)
                    {
                        return Continuation.Issue;
                    }

                    execution = admission.AuthoritativeExecution;
                    checkpoint = admission.Checkpoint;
                    currentReference = execution.CurrentReference;
                    state = GetState(currentReference);
                    if (state is not LifecycleExecutionState.Exiting
                        and not LifecycleExecutionState.Recovering)
                    {
                        throw new InvalidOperationException(
                            $"Play Mode exit cannot recover its side effect from state '{currentReference.State.Value}'.");
                    }
                }

                if ((state is LifecycleExecutionState.Exiting
                        or LifecycleExecutionState.Recovering)
                    && checkpoint?.SideEffectAdmitted != true)
                {
                    if (checkpoint == null)
                    {
                        throw new InvalidOperationException(
                            "Play Mode exit side-effect admission checkpoint disappeared.");
                    }

                    LifecycleExecutionSideEffectAdmissionCoordinator.Resolution<
                        PlayExitLifecycleExecutionCheckpoint> admission;
                    admission = await sideEffectAdmission.ReconnectAsync(
                        Kind,
                        execution,
                        claimantEndpointRegistrationGenerationId,
                        checkpointStore,
                        checkpoint,
                        cancellationToken);
                    if (admission.State
                        == LifecycleExecutionSideEffectAdmissionCoordinator
                            .Outcome.Terminal)
                    {
                        return Continuation.Terminal;
                    }
                    if (admission.State
                        == LifecycleExecutionSideEffectAdmissionCoordinator
                            .Outcome.Acquired)
                    {
                        return Continuation.Issue;
                    }

                    execution = admission.AuthoritativeExecution;
                    checkpoint = admission.Checkpoint;
                    currentReference = execution.CurrentReference;
                    state = GetState(currentReference);
                    if (state is not LifecycleExecutionState.Exiting
                        and not LifecycleExecutionState.Recovering)
                    {
                        throw new InvalidOperationException(
                            $"Play Mode exit cannot recover its side effect from state '{currentReference.State.Value}'.");
                    }
                }

                if (state == LifecycleExecutionState.Exiting)
                {
                    var outcome = await executionStore.TryEnterRecoveryAsync(
                        Kind,
                        executionId,
                        cancellationToken);
                    if (outcome
                        is LifecycleExecutionRecoveryTransitionOutcome.Entered
                            or LifecycleExecutionRecoveryTransitionOutcome
                                .AlreadyRecovering)
                    {
                        return Continuation.Recover;
                    }
                    if (outcome
                        == LifecycleExecutionRecoveryTransitionOutcome
                            .TerminalOrPublishing)
                    {
                        return Continuation.Terminal;
                    }
                    if (outcome
                        == LifecycleExecutionRecoveryTransitionOutcome
                            .SideEffectAdmissionRequired)
                    {
                        throw new InvalidOperationException(
                            "Play Mode exit recovery cannot precede its side-effect admission.");
                    }
                    throw new InvalidOperationException(
                        "Play Mode exit execution disappeared while entering recovery.");
                }

                if (state == LifecycleExecutionState.Recovering)
                {
                    return Continuation.Recover;
                }

                throw new InvalidOperationException(
                    $"Play Mode exit cannot continue from state '{currentReference.State.Value}'.");
            }
        }

        private async ValueTask<PlayExitLifecycleExecutionCheckpoint> EnsureCheckpointAsync (
            Guid executionId,
            UnityEditorObservation before)
        {
            var createResult = await checkpointStore.CreateOrReadAsync(
                executionId,
                before,
                CancellationToken.None);
            return createResult.Checkpoint;
        }

        private async ValueTask<PlayExitLifecycleExecutionOutcome>
            FinalizeDeadlineAndCreateOutcomeAsync (
            Guid executionId,
            StoredLifecycleExecution execution,
            PlayExitLifecycleExecutionCheckpoint checkpoint)
        {
            checkpoint ??= await EnsureCheckpointAsync(
                executionId,
                provider.CaptureObservation());
            return await FinalizeAndCreateOutcomeAsync(
                executionId,
                CreateSchedulerRejectionCandidate(
                    execution.Start,
                    LifecycleExecutionTerminalReason.DeadlineExceeded,
                    GetSchedulerApplicationState(execution, checkpoint),
                    canAttributeCurrentProviderObservation: true),
                execution.CurrentReference);
        }

        private async ValueTask FinalizeDeadlineWithoutResponseAsync (
            Guid executionId,
            StoredLifecycleExecution execution,
            PlayExitLifecycleExecutionCheckpoint checkpoint,
            bool canAttributeCurrentProviderObservation)
        {
            if (checkpoint == null
                && canAttributeCurrentProviderObservation)
            {
                checkpoint = await EnsureCheckpointAsync(
                    executionId,
                    provider.CaptureObservation());
            }
            await FinalizeWithoutResponseAsync(
                executionId,
                CreateSchedulerRejectionCandidate(
                    execution.Start,
                    LifecycleExecutionTerminalReason.DeadlineExceeded,
                    GetSchedulerApplicationState(execution, checkpoint),
                    canAttributeCurrentProviderObservation),
                execution.CurrentReference,
                CancellationToken.None);
        }

        private async ValueTask<PlayExitLifecycleExecutionOutcome>
            FinalizeAndCreateOutcomeAsync (
            Guid executionId,
            TerminalCandidate candidate,
            ExecutionRef authoritativeReconnectableReference)
        {
            return await PublishAndCreateOutcomeAsync(
                executionId,
                candidate,
                authoritativeReconnectableReference);
        }

        private async ValueTask<PlayExitLifecycleExecutionOutcome>
            PublishAndCreateOutcomeAsync (
            Guid executionId,
            TerminalCandidate candidate,
            ExecutionRef authoritativeReconnectableReference)
        {
            var publication = await PublishCandidateAsync(
                executionId,
                candidate,
                authoritativeReconnectableReference,
                CancellationToken.None);
            if (publication
                is TerminalPublication.PublicationFailed publicationFailed)
            {
                return PlayExitLifecycleExecutionOutcome.Failed(
                    LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                    TerminalPublicationFailureMessage,
                    publicationFailed.ReconnectableReference,
                    publicationFailed.TerminalRecord.ApplicationState,
                    publicationFailed.TerminalRecord.Result);
            }
            if (publication is TerminalPublication.Verified verified)
            {
                return CreateTerminalOutcome(
                    verified.TerminalReference,
                    verified.TerminalRecord,
                    candidate);
            }

            var unavailable = (TerminalPublication.Unavailable)publication;
            return PlayExitLifecycleExecutionOutcome.Failed(
                LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                TerminalPublicationFailureMessage,
                unavailable.ReconnectableReference,
                candidate.ApplicationState,
                candidate.Result);
        }

        private async ValueTask<PlayExitLifecycleExecutionOutcome>
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
                return PlayExitLifecycleExecutionOutcome.Failed(
                    LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                    TerminalPublicationFailureMessage,
                    publicationFailed.ReconnectableReference,
                    publicationFailed.TerminalRecord.ApplicationState,
                    publicationFailed.TerminalRecord.Result);
            }
            if (publication is TerminalPublication.Verified verified)
            {
                return CreateTerminalOutcome(
                    verified.TerminalReference,
                    verified.TerminalRecord);
            }

            var unavailable = (TerminalPublication.Unavailable)publication;
            return PlayExitLifecycleExecutionOutcome.Failed(
                LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                TerminalPublicationFailureMessage,
                unavailable.ReconnectableReference,
                ExecutionApplicationState.Indeterminate,
                result: null);
        }

        private async ValueTask FinalizeWithoutResponseAsync (
            Guid executionId,
            TerminalCandidate candidate,
            ExecutionRef authoritativeReconnectableReference,
            CancellationToken cancellationToken)
        {
            await terminalPublication.TryPublishDuringRecoveryAsync(
                executionId,
                authoritativeReconnectableReference,
                start => CreateTerminalRecord(start, candidate),
                cancellationToken);
        }

        private async ValueTask<TerminalPublication>
            PublishCandidateAsync (
            Guid executionId,
            TerminalCandidate candidate,
            ExecutionRef authoritativeReconnectableReference,
            CancellationToken cancellationToken)
        {
            return await terminalPublication.PublishAsync(
                executionId,
                authoritativeReconnectableReference,
                start => CreateTerminalRecord(start, candidate),
                cancellationToken);
        }

        private static PlayExitLifecycleExecutionTerminalRecord
            CreateTerminalRecord (
            LifecycleExecutionStartBinding start,
            TerminalCandidate candidate)
        {
            return new PlayExitLifecycleExecutionTerminalRecord(
                start.LifecycleExecutionRef.Id,
                start.LifecycleExecutionRef.DefinitionDigest,
                start.Project,
                start.Host,
                start.StartedGeneration,
                candidate.TerminalGeneration,
                start.DeadlineUtc,
                start.StartedAtUtc,
                candidate.CompletedAtUtc,
                candidate.TerminalReason,
                candidate.ApplicationState,
                candidate.Result == null
                    ? null
                    : PlayExitLifecycleTransitionResult.FromProviderResult(
                        candidate.Result),
                verdict: null,
                Array.Empty<ArtifactRef>());
        }

        private async ValueTask<StoredLifecycleExecution> RequireOpenExecutionAsync (
            Guid executionId,
            CancellationToken cancellationToken)
        {
            var execution = await executionStore.ReadAsync(
                Kind,
                executionId,
                cancellationToken);
            if (execution == null || execution.IsTerminal)
            {
                throw new InvalidOperationException(
                    "Play Mode exit Lifecycle Execution was not open.");
            }

            return execution;
        }

        private static TerminalCandidate
            CreateTerminalCandidate (
                LifecycleExecutionStartBinding start,
                PlayExitTransitionExecutionResult result,
                LifecycleExecutionTerminalReason? forcedReason = null)
        {
            var terminalReason = forcedReason
                ?? (result.IsSuccess
                    ? LifecycleExecutionTerminalReason.Completed
                    : result.Result.Result == PlayLifecycleTransitionOutcome.Timeout
                        ? LifecycleExecutionTerminalReason.DeadlineExceeded
                        : LifecycleExecutionTerminalReason.ActionFailed);
            var applicationState = result.Result.OutcomeApplicationState;
            var terminalGeneration = result.Result.After?.State.Generations
                ?? result.Result.Observed?.State.Generations;
            var error = result.Error == null
                ? null
                : new PlayExitLifecycleExecutionError(
                    result.Error.Code,
                    result.Error.Message,
                    instancePath: null);
            var candidate = new TerminalCandidate(
                start.LifecycleExecutionRef.Id,
                DateTimeOffset.UtcNow,
                terminalReason,
                applicationState,
                terminalGeneration,
                result.Result,
                error);
            return ResolveTerminalCandidate(start, candidate);
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
            var error = candidate.Error;
            if (terminalFacts.TerminalReason
                    == LifecycleExecutionTerminalReason.DeadlineExceeded
                && candidate.TerminalReason
                    != LifecycleExecutionTerminalReason.DeadlineExceeded)
            {
                error = new PlayExitLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.DeadlineExceeded,
                    "Unity Play Mode exit reached its durable execution deadline.",
                    null);
            }
            else if (generationWasRejected)
            {
                error = new PlayExitLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.GenerationMismatch,
                    "Play Mode exit terminal Editor generation regressed from its durable start.",
                    null);
            }

            return new TerminalCandidate(
                candidate.ExecutionId,
                terminalFacts.CompletedAtUtc,
                terminalFacts.TerminalReason,
                terminalFacts.ApplicationState,
                terminalFacts.TerminalGeneration,
                generationWasRejected
                    ? null
                    : candidate.Result,
                error);
        }

        private TerminalCandidate
            CreateSchedulerRejectionCandidate (
                LifecycleExecutionStartBinding start,
                LifecycleExecutionTerminalReason terminalReason,
                ExecutionApplicationState applicationState,
                bool canAttributeCurrentProviderObservation)
        {
            if (terminalReason
                is LifecycleExecutionTerminalReason.Completed
                or LifecycleExecutionTerminalReason.ActionFailed)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(terminalReason),
                    terminalReason,
                    "Scheduler rejection requires a deadline, host, project, generation, or Unity-exit reason.");
            }

            var terminalGeneration =
                canAttributeCurrentProviderObservation
                    && terminalReason
                    == LifecycleExecutionTerminalReason.DeadlineExceeded
                        ? CaptureDeadlineTerminalGeneration()
                        : null;
            return ResolveTerminalCandidate(
                start,
                new TerminalCandidate(
                    start.LifecycleExecutionRef.Id,
                    DateTimeOffset.UtcNow,
                    terminalReason,
                    applicationState,
                    terminalGeneration,
                    null,
                    CreateRecoveryError(terminalReason)));
        }

        private UnityEditorGenerationSnapshot
            CaptureDeadlineTerminalGeneration ()
        {
            return provider.CaptureObservation().State.Generations;
        }

        private static PlayExitLifecycleExecutionError CreateRecoveryError (
            LifecycleExecutionTerminalReason terminalReason)
        {
            return terminalReason switch
            {
                LifecycleExecutionTerminalReason.DeadlineExceeded => new PlayExitLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.DeadlineExceeded,
                    "Play Mode exit reached its durable execution deadline.",
                    null),
                LifecycleExecutionTerminalReason.ProjectMismatch => new PlayExitLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.ProjectMismatch,
                    "Play Mode exit recovery project does not match its durable start.",
                    null),
                LifecycleExecutionTerminalReason.HostMismatch => new PlayExitLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.HostMismatch,
                    "Play Mode exit recovery host does not match its durable start.",
                    null),
                LifecycleExecutionTerminalReason.GenerationMismatch => new PlayExitLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.GenerationMismatch,
                    "Play Mode exit recovery generation was not a proven successor.",
                    null),
                LifecycleExecutionTerminalReason.UnityExited => new PlayExitLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.UnityExited,
                    "The Unity Editor hosting Play Mode exit ended before completion.",
                    null),
                _ => new PlayExitLifecycleExecutionError(
                    PlayModeErrorCodes.PlayModeExitRejected,
                    "Play Mode exit recovery ended with an explicit action failure.",
                    null),
            };
        }

        private static LifecycleExecutionState GetState (
            ExecutionRef executionReference)
        {
            if (!TextVocabulary.TryGetValue(
                    executionReference.State.Value,
                    out LifecycleExecutionState state))
            {
                throw new InvalidOperationException(
                    $"Play Mode exit reference state is invalid: '{executionReference.State.Value}'.");
            }

            return state;
        }

        private static PlayExitLifecycleExecutionOutcome CreateTerminalOutcome (
            TerminalExecutionRef terminalReference,
            PlayExitLifecycleExecutionTerminalRecord terminalRecord,
            TerminalCandidate candidate = null)
        {
            if (terminalReference == null)
            {
                return PlayExitLifecycleExecutionOutcome.Failed(
                    UcliCoreErrorCodes.InternalError,
                    "Play Mode exit terminal reference is unavailable.",
                    lifecycleExecutionRef: null,
                    ExecutionApplicationState.NotApplied,
                    result: null,
                    hasActionPayload: false);
            }
            if (terminalRecord == null)
            {
                return PlayExitLifecycleExecutionOutcome.Failed(
                    UcliCoreErrorCodes.InternalError,
                    "Play Mode exit terminal record is unavailable.",
                    lifecycleExecutionRef: null,
                    ExecutionApplicationState.Indeterminate,
                    result: null,
                    hasActionPayload: false);
            }

            if (terminalRecord.TerminalReason
                    == LifecycleExecutionTerminalReason.Completed
                && terminalRecord.Result?.IsSuccessful == true)
            {
                return PlayExitLifecycleExecutionOutcome.Completed(
                    terminalReference,
                    terminalRecord.Result);
            }

            var error = TerminalCandidateMatchesRecord(
                    candidate,
                    terminalRecord)
                ? candidate.Error
                : CreateRecoveryError(terminalRecord.TerminalReason);
            return PlayExitLifecycleExecutionOutcome.Failed(
                error.Code,
                error.Message,
                terminalReference,
                terminalRecord.ApplicationState,
                terminalRecord.Result,
                error.InstancePath);
        }

        private static ExecutionApplicationState GetSchedulerApplicationState (
            StoredLifecycleExecution execution,
            PlayExitLifecycleExecutionCheckpoint checkpoint)
        {
            return LifecycleExecutionTerminalFactsPolicy
                .ResolveUnprovenApplicationState(
                    execution.CurrentReference,
                    checkpoint?.SideEffectAdmitted == true);
        }

        private static bool TerminalCandidateMatchesRecord (
            TerminalCandidate candidate,
            PlayExitLifecycleExecutionTerminalRecord terminalRecord)
        {
            if (candidate == null)
            {
                return false;
            }

            var expectedResult = candidate.Result == null
                ? null
                : PlayExitLifecycleTransitionResult.FromProviderResult(
                    candidate.Result);
            return candidate.ExecutionId == terminalRecord.ExecutionId
                && candidate.TerminalReason == terminalRecord.TerminalReason
                && candidate.ApplicationState == terminalRecord.ApplicationState
                && candidate.TerminalGeneration == terminalRecord.TerminalGeneration
                && candidate.CompletedAtUtc == terminalRecord.CompletedAtUtc
                && expectedResult == terminalRecord.Result;
        }

        private sealed record TerminalCandidate (
            Guid ExecutionId,
            DateTimeOffset CompletedAtUtc,
            LifecycleExecutionTerminalReason TerminalReason,
            ExecutionApplicationState ApplicationState,
            UnityEditorGenerationSnapshot TerminalGeneration,
            PlayLifecycleTransitionResult Result,
            PlayExitLifecycleExecutionError Error);

        private enum Continuation
        {
            Issue = 1,
            Recover,
            Terminal,
        }
    }
}
