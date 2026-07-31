using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using AttemptResolution =
    MackySoft.Ucli.Unity.Ipc.LifecycleExecutionAttemptResolution;
using TerminalPublication =
    MackySoft.Ucli.Unity.Ipc.LifecycleExecutionTerminalPublication<
        MackySoft.Ucli.Contracts.Execution.Lifecycle.PlayEnterLifecycleExecutionTerminalRecord>;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Owns the typed <c>play.enter</c> state machine from side-effect admission through terminal publication.
    /// </summary>
    internal sealed class PlayEnterLifecycleExecutionHandler :
        IPlayEnterLifecycleExecutionHandler,
        ILifecycleExecutionRecoveryHandler
    {
        private const string TerminalPublicationFailureMessage =
            "Play Mode entry terminal record could not be published and reverified.";

        private readonly IPlayEnterLifecycleExecutionProvider provider;
        private readonly FileLifecycleExecutionStore executionStore;
        private readonly LifecycleExecutionAttemptBoundary attemptBoundary;
        private readonly FilePlayEnterLifecycleExecutionCheckpointStore checkpointStore;
        private readonly LifecycleExecutionTerminalPublicationBoundary<
            PlayEnterLifecycleExecutionTerminalRecord>
            terminalPublication;

        public PlayEnterLifecycleExecutionHandler (
            IPlayEnterLifecycleExecutionProvider provider,
            FileLifecycleExecutionStore executionStore,
            FilePlayEnterLifecycleExecutionCheckpointStore checkpointStore,
            IDaemonLogger daemonLogger)
        {
            this.provider = provider
                ?? throw new ArgumentNullException(nameof(provider));
            this.executionStore = executionStore
                ?? throw new ArgumentNullException(nameof(executionStore));
            attemptBoundary = new(this.executionStore);
            this.checkpointStore = checkpointStore
                ?? throw new ArgumentNullException(nameof(checkpointStore));
            terminalPublication =
                new LifecycleExecutionTerminalPublicationBoundary<
                    PlayEnterLifecycleExecutionTerminalRecord>(
                    Kind,
                    this.executionStore,
                    daemonLogger
                        ?? throw new ArgumentNullException(nameof(daemonLogger)),
                    "Play Mode entry terminal publication failed.",
                    "Play Mode entry terminal publication failed during recovery.");
        }

        public LifecycleExecutionKind Kind => LifecycleExecutionKind.PlayEnter;

        public async ValueTask<PlayEnterLifecycleExecutionOutcome> ExecuteAsync (
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
                return PlayEnterLifecycleExecutionOutcome.Failed(
                    UcliCoreErrorCodes.InvalidArgument,
                    "Play Mode entry start record was not found.",
                    lifecycleExecutionRef: null,
                    ExecutionApplicationState.NotApplied,
                    result: null);
            }

            if (attemptResolution
                is AttemptResolution.BindingMismatch bindingMismatch)
            {
                return PlayEnterLifecycleExecutionOutcome.Failed(
                    LifecycleExecutionStartBindingMatcher
                        .GetMismatchErrorCode(bindingMismatch.Match),
                    "Play Mode entry request does not match its durable start binding.",
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
            var stored = attemptResolution switch
            {
                AttemptResolution.Open open => open.Execution,
                AttemptResolution.DeadlineExceeded deadline =>
                    deadline.Execution,
                _ => throw new InvalidOperationException(
                    "Play Mode entry attempt boundary returned an unsupported resolution."),
            };
            var checkpoint = await checkpointStore.ReadAsync(
                executionId,
                CancellationToken.None);
            TerminalCandidate terminalCandidate;
            if (attemptResolution
                is AttemptResolution.DeadlineExceeded)
            {
                terminalCandidate = CreateSchedulerRejectionCandidate(
                    stored.Start,
                    LifecycleExecutionTerminalReason.DeadlineExceeded,
                    checkpoint,
                    canAttributeCurrentProviderObservation: true);
            }
            else
            {
                var executionContext = new PlayEnterLifecycleExecutionContext(
                    executionStore,
                    checkpointStore,
                    executionId,
                    claimantEndpointRegistrationGenerationId,
                    checkpoint,
                    enterRecoveryWhenReconnecting: false);
                var completion = await attemptBoundary.ObserveCompletionAsync(
                    Kind,
                    openAttempt,
                    provider.EnterAsync(
                        executionContext,
                        openAttempt.DeadlineCancellationToken));
                if (completion
                    is AttemptResolution.Completed<
                        PlayEnterTransitionExecutionResult> completed)
                {
                    terminalCandidate = CreateTerminalCandidate(
                        executionId,
                        completed.Result);
                }
                else
                {
                    if (completion is AttemptResolution.Missing)
                    {
                        throw new InvalidOperationException(
                            "Play Mode entry execution disappeared before deadline classification.");
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
                            "Play Mode entry cancellation returned an unsupported deadline resolution.");
                    }

                    stored = deadline.Execution;
                    checkpoint = await checkpointStore.ReadAsync(
                        executionId,
                        CancellationToken.None);
                    terminalCandidate = CreateSchedulerRejectionCandidate(
                        stored.Start,
                        LifecycleExecutionTerminalReason.DeadlineExceeded,
                        checkpoint,
                        canAttributeCurrentProviderObservation: true);
                }
            }

            terminalCandidate = ResolveTerminalCandidate(
                stored.Start,
                terminalCandidate);
            return await PublishAndCreateOutcomeAsync(
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
                    "Play Mode entry recovery attempt boundary returned an unsupported resolution."),
            };
            var checkpoint = await checkpointStore.ReadAsync(
                executionId,
                cancellationToken);
            if (request.RejectionReason.HasValue)
            {
                var rejectedCandidate = CreateSchedulerRejectionCandidate(
                    request.Start,
                    request.RejectionReason.Value,
                    checkpoint,
                    request.CanAttributeCurrentProviderObservation);
                rejectedCandidate = ResolveTerminalCandidate(
                    request.Start,
                    rejectedCandidate);
                await TryPublishDuringRecoveryAsync(
                    rejectedCandidate,
                    execution.CurrentReference,
                    cancellationToken);
                return;
            }

            if (checkpoint == null)
            {
                return;
            }

            if (attemptResolution
                is AttemptResolution.DeadlineExceeded)
            {
                var deadlineCandidate = CreateSchedulerRejectionCandidate(
                    execution.Start,
                    LifecycleExecutionTerminalReason.DeadlineExceeded,
                    checkpoint,
                    request.CanAttributeCurrentProviderObservation);
                deadlineCandidate = ResolveTerminalCandidate(
                    execution.Start,
                    deadlineCandidate);
                await TryPublishDuringRecoveryAsync(
                    deadlineCandidate,
                    execution.CurrentReference,
                    CancellationToken.None);
                return;
            }

            var executionContext = new PlayEnterLifecycleExecutionContext(
                executionStore,
                checkpointStore,
                executionId,
                claimantEndpointRegistrationGenerationId,
                checkpoint,
                enterRecoveryWhenReconnecting: true);
            if (checkpoint.SideEffectAdmitted
                && !await executionContext.TryEnterRecoveryAfterAdmissionAsync(
                    cancellationToken))
            {
                await terminalPublication.TryRecoverDuringRecoveryAsync(
                    executionId,
                    execution.CurrentReference,
                    cancellationToken);
                return;
            }

            var completion = await attemptBoundary.ObserveCompletionAsync(
                Kind,
                openAttempt,
                provider.EnterAsync(
                    executionContext,
                    openAttempt.DeadlineCancellationToken));
            TerminalCandidate terminalCandidate;
            if (completion
                is AttemptResolution.Completed<
                    PlayEnterTransitionExecutionResult> completed)
            {
                terminalCandidate = CreateTerminalCandidate(
                    executionId,
                    completed.Result);
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
                        "Play Mode entry recovery cancellation returned an unsupported deadline resolution.");
                }

                execution = deadline.Execution;
                checkpoint = await checkpointStore.ReadAsync(
                    executionId,
                    CancellationToken.None);
                terminalCandidate = CreateSchedulerRejectionCandidate(
                    execution.Start,
                    LifecycleExecutionTerminalReason.DeadlineExceeded,
                    checkpoint,
                    request.CanAttributeCurrentProviderObservation);
            }
            terminalCandidate = ResolveTerminalCandidate(
                request.Start,
                terminalCandidate);
            await TryPublishDuringRecoveryAsync(
                terminalCandidate,
                execution.CurrentReference,
                cancellationToken);
        }

        private async ValueTask<PlayEnterLifecycleExecutionOutcome>
            PublishAndCreateOutcomeAsync (
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
                return PlayEnterLifecycleExecutionOutcome.Failed(
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
                    terminalCandidate);
            }

            var unavailable = (TerminalPublication.Unavailable)publication;
            return PlayEnterLifecycleExecutionOutcome.Failed(
                LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                TerminalPublicationFailureMessage,
                unavailable.ReconnectableReference,
                terminalCandidate.ApplicationState,
                terminalCandidate.Result);
        }

        private async ValueTask<PlayEnterLifecycleExecutionOutcome>
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
                return PlayEnterLifecycleExecutionOutcome.Failed(
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
            return PlayEnterLifecycleExecutionOutcome.Failed(
                LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                TerminalPublicationFailureMessage,
                unavailable.ReconnectableReference,
                ExecutionApplicationState.Indeterminate,
                result: null);
        }

        private static PlayEnterLifecycleExecutionOutcome
            CreateTerminalOutcome (
            TerminalExecutionRef terminalReference,
            PlayEnterLifecycleExecutionTerminalRecord terminalRecord,
            TerminalCandidate terminalCandidate = null)
        {
            if (terminalRecord.TerminalReason
                    == LifecycleExecutionTerminalReason.Completed
                && terminalRecord.Result?.IsSuccessful == true)
            {
                return PlayEnterLifecycleExecutionOutcome.Completed(
                    terminalReference,
                    terminalRecord.Result);
            }

            var error = TerminalCandidateMatchesRecord(
                    terminalCandidate,
                    terminalRecord)
                ? terminalCandidate.Error
                : CreateRecoveryError(terminalRecord.TerminalReason);
            return PlayEnterLifecycleExecutionOutcome.Failed(
                error.Code,
                error.Message,
                terminalReference,
                terminalRecord.ApplicationState,
                terminalRecord.Result,
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

        private static PlayEnterLifecycleExecutionTerminalRecord
            CreateTerminalRecord (
            LifecycleExecutionStartBinding start,
            TerminalCandidate terminalCandidate)
        {
            var executionId = terminalCandidate.ExecutionId;
            return new PlayEnterLifecycleExecutionTerminalRecord(
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
                terminalCandidate.Result == null
                    ? null
                    : PlayEnterLifecycleTransitionResult.FromProviderResult(
                        terminalCandidate.Result),
                verdict: null,
                Array.Empty<ArtifactRef>());
        }

        private static TerminalCandidate
            CreateTerminalCandidate (
                Guid executionId,
                PlayEnterTransitionExecutionResult executionResult)
        {
            var transition = executionResult.Response.Transition;
            var terminalReason = executionResult.IsSuccess
                ? LifecycleExecutionTerminalReason.Completed
                : executionResult.Error.Code
                    == PlayModeErrorCodes.PlayModeTransitionTimeout
                        ? LifecycleExecutionTerminalReason.DeadlineExceeded
                        : LifecycleExecutionTerminalReason.ActionFailed;
            var applicationState = transition.OutcomeApplicationState;
            var error = executionResult.Error == null
                ? null
                : new PlayEnterLifecycleExecutionError(
                    executionResult.Error.Code,
                    executionResult.Error.Message,
                    instancePath: null);
            return new TerminalCandidate(
                executionId,
                transition,
                error,
                terminalReason,
                applicationState,
                transition.After?.State.Generations
                    ?? transition.Observed?.State.Generations,
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
            var error = candidate.Error;
            if (terminalFacts.TerminalReason
                    == LifecycleExecutionTerminalReason.DeadlineExceeded
                && candidate.TerminalReason
                    != LifecycleExecutionTerminalReason.DeadlineExceeded)
            {
                error = new PlayEnterLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.DeadlineExceeded,
                    "Unity Play Mode enter reached its durable execution deadline.",
                    null);
            }
            else if (generationWasRejected)
            {
                error = new PlayEnterLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.GenerationMismatch,
                    "Play Mode entry terminal Editor generation regressed from its durable start.",
                    null);
            }

            return new TerminalCandidate(
                candidate.ExecutionId,
                generationWasRejected
                    ? null
                    : candidate.Result,
                error,
                terminalFacts.TerminalReason,
                terminalFacts.ApplicationState,
                terminalFacts.TerminalGeneration,
                terminalFacts.CompletedAtUtc);
        }

        private TerminalCandidate
            CreateSchedulerRejectionCandidate (
                LifecycleExecutionStartBinding start,
                LifecycleExecutionTerminalReason reason,
                PlayEnterLifecycleExecutionCheckpoint checkpoint,
                bool canAttributeCurrentProviderObservation)
        {
            var sideEffectAdmitted =
                checkpoint?.SideEffectAdmitted == true;
            var applicationState =
                LifecycleExecutionTerminalFactsPolicy
                    .ResolveUnprovenApplicationState(
                    start.LifecycleExecutionRef,
                    sideEffectAdmitted);
            var terminalGeneration =
                canAttributeCurrentProviderObservation
                    && reason
                        == LifecycleExecutionTerminalReason.DeadlineExceeded
                    ? CaptureDeadlineTerminalGeneration()
                    : null;
            return new TerminalCandidate(
                start.LifecycleExecutionRef.Id,
                null,
                CreateRecoveryError(reason),
                reason,
                applicationState,
                terminalGeneration,
                DateTimeOffset.UtcNow);
        }

        private UnityEditorGenerationSnapshot
            CaptureDeadlineTerminalGeneration ()
        {
            return provider.CaptureObservation().State.Generations;
        }

        private static PlayEnterLifecycleExecutionError CreateRecoveryError (
            LifecycleExecutionTerminalReason reason)
        {
            return reason switch
            {
                LifecycleExecutionTerminalReason.DeadlineExceeded => new PlayEnterLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.DeadlineExceeded,
                    "Play Mode entry reached its durable execution deadline.",
                    null),
                LifecycleExecutionTerminalReason.ProjectMismatch => new PlayEnterLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.ProjectMismatch,
                    "Play Mode entry recovery project does not match its durable start.",
                    null),
                LifecycleExecutionTerminalReason.HostMismatch => new PlayEnterLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.HostMismatch,
                    "Play Mode entry recovery host does not match its durable start.",
                    null),
                LifecycleExecutionTerminalReason.GenerationMismatch => new PlayEnterLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.GenerationMismatch,
                    "Play Mode entry recovery generation was not a proven successor.",
                    null),
                LifecycleExecutionTerminalReason.UnityExited => new PlayEnterLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.UnityExited,
                    "The Unity Editor hosting Play Mode entry exited before completion.",
                    null),
                _ => new PlayEnterLifecycleExecutionError(
                    PlayModeErrorCodes.PlayModeEnterRejected,
                    "Play Mode entry recovery ended with an explicit action failure.",
                    null),
            };
        }

        private static bool TerminalCandidateMatchesRecord (
            TerminalCandidate candidate,
            PlayEnterLifecycleExecutionTerminalRecord terminalRecord)
        {
            if (candidate == null)
            {
                return false;
            }

            var expectedResult = candidate.Result == null
                ? null
                : PlayEnterLifecycleTransitionResult.FromProviderResult(
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
            PlayLifecycleTransitionResult Result,
            PlayEnterLifecycleExecutionError Error,
            LifecycleExecutionTerminalReason TerminalReason,
            ExecutionApplicationState ApplicationState,
            UnityEditorGenerationSnapshot TerminalGeneration,
            DateTimeOffset CompletedAtUtc);

    }
}
