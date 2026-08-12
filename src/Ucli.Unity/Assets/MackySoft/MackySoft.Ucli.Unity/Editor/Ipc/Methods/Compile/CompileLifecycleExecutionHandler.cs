using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Runtime;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using AttemptResolution =
    MackySoft.Ucli.Unity.Ipc.LifecycleExecutionAttemptResolution;
using TerminalPublication =
    MackySoft.Ucli.Unity.Ipc.LifecycleExecutionTerminalPublication<
        MackySoft.Ucli.Contracts.Execution.Lifecycle.CompileLifecycleExecutionTerminalRecord>;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Owns the typed <c>compile</c> state machine from refresh admission through terminal publication.
    /// </summary>
    internal sealed class CompileLifecycleExecutionHandler :
        ICompileLifecycleExecutionHandler,
        ILifecycleExecutionRecoveryHandler
    {
        private const int RequiredStableLifecycleObservations = 2;

        private const string TerminalPublicationFailureMessage =
            "Compile terminal record could not be published and reverified.";

        private readonly ICompileLifecycleExecutionProvider provider;
        private readonly FileLifecycleExecutionStore executionStore;
        private readonly ILifecycleExecutionTimeSource timeSource;
        private readonly LifecycleExecutionAttemptBoundary attemptBoundary;
        private readonly FileCompileLifecycleExecutionCheckpointStore checkpointStore;
        private readonly LifecycleExecutionSideEffectAdmissionCoordinator
            sideEffectAdmission;
        private readonly LifecycleExecutionTerminalPublicationBoundary<
            CompileLifecycleExecutionTerminalRecord>
            terminalPublication;

        public CompileLifecycleExecutionHandler (
            ICompileLifecycleExecutionProvider provider,
            IDaemonLogger daemonLogger,
            FileLifecycleExecutionStore executionStore,
            FileCompileLifecycleExecutionCheckpointStore checkpointStore,
            ILifecycleExecutionTimeSource timeSource)
        {
            this.provider = provider
                ?? throw new ArgumentNullException(nameof(provider));
            this.executionStore = executionStore
                ?? throw new ArgumentNullException(nameof(executionStore));
            this.timeSource = timeSource
                ?? throw new ArgumentNullException(nameof(timeSource));
            attemptBoundary = new(this.executionStore, this.timeSource);
            this.checkpointStore = checkpointStore
                ?? throw new ArgumentNullException(nameof(checkpointStore));
            sideEffectAdmission =
                new LifecycleExecutionSideEffectAdmissionCoordinator(
                    this.executionStore);
            terminalPublication =
                new LifecycleExecutionTerminalPublicationBoundary<
                    CompileLifecycleExecutionTerminalRecord>(
                    Kind,
                    this.executionStore,
                    daemonLogger
                        ?? throw new ArgumentNullException(nameof(daemonLogger)),
                    "Compile terminal publication failed.",
                    "Compile terminal publication failed during recovery.");
        }

        public LifecycleExecutionKind Kind => LifecycleExecutionKind.Compile;

        public async ValueTask<CompileLifecycleExecutionOutcome> ExecuteAsync (
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
                    "Compile start record was not found.",
                    lifecycleExecutionRef: null,
                    ExecutionApplicationState.NotApplied,
                    result: null);
            }
            if (attemptResolution
                is AttemptResolution.BindingMismatch bindingMismatch)
            {
                return CreateErrorResponse(
                    LifecycleExecutionStartBindingMatcher
                        .GetMismatchErrorCode(bindingMismatch.Match),
                    "Compile request does not match its durable start binding.",
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
                    "Compile attempt boundary returned an unsupported resolution."),
            };
            var checkpoint = await checkpointStore.ReadAsync(
                executionId,
                CancellationToken.None);
            TerminalCandidate terminalCandidate;
            if (attemptResolution
                is AttemptResolution.DeadlineExceeded)
            {
                terminalCandidate = CreateDeadlineCandidate(
                    executionId,
                    checkpoint,
                    stored.CurrentReference,
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
                            "Compile execution disappeared before deadline classification.");
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
                            "Compile cancellation returned an unsupported deadline resolution.");
                    }

                    stored = deadline.Execution;
                    checkpoint = await checkpointStore.ReadAsync(
                        executionId,
                        CancellationToken.None);
                    terminalCandidate = CreateDeadlineCandidate(
                        executionId,
                        checkpoint,
                        stored.CurrentReference,
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
            var stored = attemptResolution switch
            {
                AttemptResolution.Open open => open.Execution,
                AttemptResolution.DeadlineExceeded deadline =>
                    deadline.Execution,
                _ => throw new InvalidOperationException(
                    "Compile recovery attempt boundary returned an unsupported resolution."),
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
                    stored?.CurrentReference,
                    request.CanAttributeCurrentProviderObservation);
                rejectionCandidate = ResolveTerminalCandidate(
                    request.Start,
                    rejectionCandidate);
                await TryPublishDuringRecoveryAsync(
                    rejectionCandidate,
                    stored.CurrentReference,
                    cancellationToken);
                return;
            }
            if (checkpoint == null)
            {
                if (attemptResolution
                    is AttemptResolution.DeadlineExceeded)
                {
                    var deadlineCandidate = CreateDeadlineCandidate(
                        executionId,
                        checkpoint: null,
                        currentReference: stored.CurrentReference,
                        canAttributeCurrentProviderObservation:
                            request.CanAttributeCurrentProviderObservation);
                    deadlineCandidate = ResolveTerminalCandidate(
                        stored.Start,
                        deadlineCandidate);
                    await TryPublishDuringRecoveryAsync(
                        deadlineCandidate,
                        stored.CurrentReference,
                        CancellationToken.None);
                }

                return;
            }

            TerminalCandidate terminalCandidate;
            if (attemptResolution
                is AttemptResolution.DeadlineExceeded)
            {
                terminalCandidate = CreateDeadlineCandidate(
                    executionId,
                    checkpoint,
                    stored?.CurrentReference,
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
                            "Compile recovery cancellation returned an unsupported deadline resolution.");
                    }

                    stored = deadline.Execution;
                    checkpoint = await checkpointStore.ReadAsync(
                        executionId,
                        CancellationToken.None);
                    terminalCandidate = CreateDeadlineCandidate(
                        executionId,
                        checkpoint,
                        stored.CurrentReference,
                        request.CanAttributeCurrentProviderObservation);
                }
            }

            if (terminalCandidate == null)
            {
                await terminalPublication.TryRecoverDuringRecoveryAsync(
                    executionId,
                    stored.CurrentReference,
                    CancellationToken.None);
                return;
            }
            terminalCandidate = ResolveTerminalCandidate(
                request.Start,
                terminalCandidate);
            await TryPublishDuringRecoveryAsync(
                terminalCandidate,
                stored.CurrentReference,
                CancellationToken.None);
        }

        private async Task<TerminalCandidate> ExecuteAsync (
            Guid executionId,
            CompileLifecycleExecutionCheckpoint checkpoint,
            Guid claimantEndpointRegistrationGenerationId,
            CancellationToken executionCancellationToken,
            bool enterRecoveryWhenReconnecting)
        {
            if (checkpoint == null)
            {
                var beforeSnapshot = provider.CaptureObservation();
                var beforeObservation = CreateObservation(beforeSnapshot);
                var pendingResult = CreatePendingResult(
                    beforeSnapshot,
                    timeSource.UtcNow);
                checkpoint = await checkpointStore.WritePreparedAsync(
                    executionId,
                    beforeObservation,
                    pendingResult,
                    executionCancellationToken);
            }

            var admission = await ResolveSideEffectAdmissionAsync(
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
                    .Acquired)
            {
                var mutationActivity = provider.BeginMutation();
                IDisposable diagnosticsObservation = null;
                Task<UnityEditorRuntimeObservation> settleTask = null;
                try
                {
                    diagnosticsObservation =
                        provider.BeginDiagnosticsObservation(
                            checkpointStore.CreateDiagnosticsSink(
                                executionId));
                    settleTask = WaitUntilCompileSettledAsync(
                        executionId,
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
                                timeSource.UtcNow,
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
                        return CreateActionFailureCandidate(
                            executionId,
                            checkpoint);
                    }
                    checkpoint = await checkpointStore.MarkProviderReturnedAsync(
                        checkpoint,
                        timeSource.UtcNow,
                        CancellationToken.None);
                    var afterSnapshot = await AwaitWithCancellationAsync(
                        settleTask,
                        executionCancellationToken);
                    checkpoint = await CompleteDiagnosticsCheckpointAsync(
                        executionId,
                        afterSnapshot);
                    await TryMarkCompilingStateFromEvidenceAsync(
                        checkpoint,
                        afterSnapshot,
                        CancellationToken.None);
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

                    diagnosticsObservation?.Dispose();
                }
            }

            if (enterRecoveryWhenReconnecting
                && !await TryEnterRecoveryAfterAdmissionAsync(
                    executionId,
                    executionCancellationToken))
            {
                return null;
            }

            var recoveredDiagnosticsObservation =
                provider.BeginDiagnosticsObservation(
                    checkpointStore.CreateDiagnosticsSink(executionId));
            try
            {
                var afterSnapshot = await WaitUntilRecoveredCompileSettledAsync(
                    provider,
                    checkpoint,
                    executionCancellationToken);
                checkpoint = await CompleteDiagnosticsCheckpointAsync(
                    executionId,
                    afterSnapshot);
                await TryMarkCompilingStateFromEvidenceAsync(
                    checkpoint,
                    afterSnapshot,
                    CancellationToken.None);
                return CreateCompletedCandidate(
                    executionId,
                    checkpoint,
                    afterSnapshot);
            }
            finally
            {
                recoveredDiagnosticsObservation.Dispose();
                }
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
                        "Compile recovery cannot precede its refresh side-effect admission."),
                LifecycleExecutionRecoveryTransitionOutcome.Missing =>
                    throw new InvalidOperationException(
                        "Compile execution disappeared while entering recovery."),
                _ => throw new InvalidOperationException(
                    $"Compile recovery transition could not classify outcome '{outcome}'."),
            };
        }

        private async ValueTask<
                LifecycleExecutionSideEffectAdmissionCoordinator.Resolution<
                    CompileLifecycleExecutionCheckpoint>>
            ResolveSideEffectAdmissionAsync (
            CompileLifecycleExecutionCheckpoint checkpoint,
            Guid claimantEndpointRegistrationGenerationId,
            CancellationToken cancellationToken)
        {
            var stored = await executionStore.ReadAsync(
                Kind,
                checkpoint.ExecutionId,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "Compile execution disappeared before side-effect admission.");
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
                        "Compile side-effect admission marker cannot precede its durable execution right.");
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
                    $"Compile execution state '{stored.CurrentReference.State.Value}' cannot admit or recover its refresh side effect.");
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
                CompileLifecycleExecutionCheckpoint> resolution)
        {
            if (resolution.State
                    == LifecycleExecutionSideEffectAdmissionCoordinator.Outcome
                        .Recover
                && !CanRecoverWithoutDispatch(
                    resolution.AuthoritativeExecution.CurrentReference))
            {
                throw new InvalidOperationException(
                    $"Compile execution state '{resolution.AuthoritativeExecution.CurrentReference.State.Value}' cannot recover its refresh side effect.");
            }
        }

        private async ValueTask TryMarkCompilingStateFromEvidenceAsync (
            CompileLifecycleExecutionCheckpoint checkpoint,
            UnityEditorRuntimeObservation observation,
            CancellationToken cancellationToken)
        {
            if (!HasCompilationEvidence(checkpoint, observation))
            {
                return;
            }

            var stored = await executionStore.ReadAsync(
                Kind,
                checkpoint.ExecutionId,
                cancellationToken);
            if (stored == null
                || stored.IsTerminal
                || IsActionState(
                    stored.CurrentReference,
                    LifecycleExecutionState.Compiling)
                || !IsActionState(
                    stored.CurrentReference,
                    LifecycleExecutionState.Refreshing))
            {
                return;
            }

            var next = LifecycleExecutionReferenceFactory.CreateStateProjection(
                stored.CurrentReference,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Compiling);
            _ = await executionStore.TryUpdateReferenceAsync(
                stored.CurrentReference,
                next,
                cancellationToken);
        }

        private async ValueTask<CompileLifecycleExecutionOutcome>
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
                return CreateErrorResponse(
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
            return CreateErrorResponse(
                LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                TerminalPublicationFailureMessage,
                unavailable.ReconnectableReference,
                terminalCandidate.ApplicationState,
                terminalCandidate.Result);
        }

        private async ValueTask<CompileLifecycleExecutionOutcome>
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
                return CreateErrorResponse(
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
            return CompileLifecycleExecutionOutcome.Failed(
                LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                TerminalPublicationFailureMessage,
                unavailable.ReconnectableReference,
                ExecutionApplicationState.Indeterminate,
                result: null,
                observedLifecycle: null);
        }

        private static CompileLifecycleExecutionOutcome CreateTerminalOutcome (
            TerminalExecutionRef terminalReference,
            CompileLifecycleExecutionTerminalRecord terminalRecord)
        {
            if (terminalRecord.TerminalReason
                == LifecycleExecutionTerminalReason.Completed)
            {
                return CompileLifecycleExecutionOutcome.Completed(
                    terminalReference,
                    terminalRecord.Result);
            }

            var error = CreateRecoveryError(terminalRecord.TerminalReason);
            return CompileLifecycleExecutionOutcome.Failed(
                error.Code,
                error.Message,
                terminalReference,
                terminalRecord.ApplicationState,
                terminalRecord.Result,
                observedLifecycle: null,
                instancePath: error.InstancePath);
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

        private static CompileLifecycleExecutionTerminalRecord
            CreateTerminalRecord (
            LifecycleExecutionStartBinding start,
            TerminalCandidate terminalCandidate)
        {
            var executionId = terminalCandidate.ExecutionId;
            return new CompileLifecycleExecutionTerminalRecord(
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
                terminalCandidate.Verdict,
                Array.Empty<ArtifactRef>());
        }

        private TerminalCandidate CreateCompletedCandidate (
            Guid executionId,
            CompileLifecycleExecutionCheckpoint checkpoint,
            UnityEditorRuntimeObservation afterSnapshot)
        {
            EnsureDiagnosticsAreComplete(
                checkpoint.CurrentResult,
                afterSnapshot,
                checkpoint.Diagnostics);
            var completedAtUtc = timeSource.UtcNow;
            var result = CreateFinalResult(
                checkpoint.CurrentResult,
                afterSnapshot,
                checkpoint.Diagnostics,
                checkpoint.ProviderReturnedAtUtc
                    ?? afterSnapshot.ObservedAtUtc);
            return new TerminalCandidate(
                executionId,
                result,
                LifecycleExecutionTerminalReason.Completed,
                ExecutionApplicationState.Applied,
                afterSnapshot.State.Generations,
                completedAtUtc,
                CompileLifecycleVerdictPolicy.Evaluate(result));
        }

        private async ValueTask<CompileLifecycleExecutionCheckpoint>
            CompleteDiagnosticsCheckpointAsync (
                Guid executionId,
                UnityEditorRuntimeObservation afterSnapshot)
        {
            var checkpoint = await checkpointStore.ReadAsync(
                    executionId,
                    CancellationToken.None)
                ?? throw new IOException(
                    "Compile checkpoint disappeared after lifecycle settlement.");
            var compileGenerationAdvanced =
                checkpoint.CurrentResult.ScriptCompilation
                    .CompileGenerationBefore
                    != afterSnapshot.State.Generations.CompileGeneration;
            if (!checkpoint.Diagnostics.Started
                && !compileGenerationAdvanced
                && !checkpoint.Diagnostics.Completed)
            {
                checkpoint =
                    await checkpointStore.MarkNoCompilationRequiredAsync(
                        executionId,
                        CancellationToken.None);
            }

            return checkpoint;
        }

        private TerminalCandidate CreateDeadlineCandidate (
            Guid executionId,
            CompileLifecycleExecutionCheckpoint checkpoint,
            ExecutionRef currentReference,
            bool canAttributeCurrentProviderObservation)
        {
            var snapshot = canAttributeCurrentProviderObservation
                ? provider.CaptureObservation()
                : null;
            var result = canAttributeCurrentProviderObservation
                    && HasObservedCompileResult(checkpoint, snapshot)
                ? CreateObservedPartialResult(checkpoint, snapshot)
                : null;
            return new TerminalCandidate(
                executionId,
                result,
                LifecycleExecutionTerminalReason.DeadlineExceeded,
                LifecycleExecutionTerminalFactsPolicy
                    .ResolveUnprovenApplicationState(
                    currentReference,
                checkpoint?.SideEffectAdmitted == true),
                snapshot?.State.Generations,
                timeSource.UtcNow,
                Verdict: null);
        }

        private TerminalCandidate
            CreateActionFailureCandidate (
                Guid executionId,
                CompileLifecycleExecutionCheckpoint checkpoint)
        {
            var snapshot = provider.CaptureObservation();
            var result = checkpoint?.SideEffectAdmitted == true
                ? CreateObservedPartialResult(checkpoint, snapshot)
                : checkpoint?.CurrentResult
                    ?? CreatePendingResult(snapshot, timeSource.UtcNow);
            return new TerminalCandidate(
                executionId,
                result,
                LifecycleExecutionTerminalReason.ActionFailed,
                checkpoint?.SideEffectAdmitted == true
                    ? ExecutionApplicationState.Indeterminate
                    : ExecutionApplicationState.NotApplied,
                snapshot.State.Generations,
                timeSource.UtcNow,
                Verdict: null);
        }

        private TerminalCandidate
            CreateRecoveryRejectionCandidate (
                LifecycleExecutionStartBinding start,
                LifecycleExecutionTerminalReason reason,
                CompileLifecycleExecutionCheckpoint checkpoint,
                ExecutionRef currentReference,
                bool canAttributeCurrentProviderObservation)
        {
            var canAttributeGeneration =
                canAttributeCurrentProviderObservation
                && LifecycleExecutionTerminalFactsPolicy
                    .CanAttributeObservedGeneration(reason);
            var snapshot = canAttributeGeneration
                ? provider.CaptureObservation()
                : null;
            var result = canAttributeGeneration
                    && HasObservedCompileResult(checkpoint, snapshot)
                ? CreateObservedPartialResult(checkpoint, snapshot)
                : null;
            var sideEffectAdmitted =
                checkpoint?.SideEffectAdmitted == true;
            return new TerminalCandidate(
                start.LifecycleExecutionRef.Id,
                result,
                reason,
                LifecycleExecutionTerminalFactsPolicy
                    .ResolveUnprovenApplicationState(
                    currentReference,
                    sideEffectAdmitted),
                canAttributeGeneration
                    ? snapshot?.State.Generations
                    : null,
                timeSource.UtcNow,
                Verdict: null);
        }

        private TerminalCandidate
            ResolveTerminalCandidate (
                LifecycleExecutionStartBinding start,
                TerminalCandidate candidate)
        {
            var fixedAtUtc = timeSource.UtcNow;
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
                terminalFacts.CompletedAtUtc,
                terminalFacts.TerminalReason == candidate.TerminalReason
                    && !generationWasRejected
                    ? candidate.Verdict
                    : null);
        }

        private static CompileLifecycleExecutionError CreateRecoveryError (
            LifecycleExecutionTerminalReason reason)
        {
            return reason switch
            {
                LifecycleExecutionTerminalReason.DeadlineExceeded => new CompileLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.DeadlineExceeded,
                    "Compile reached its durable execution deadline.",
                    null),
                LifecycleExecutionTerminalReason.ProjectMismatch => new CompileLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.ProjectMismatch,
                    "Compile recovery project does not match its durable start.",
                    null),
                LifecycleExecutionTerminalReason.HostMismatch => new CompileLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.HostMismatch,
                    "Compile recovery host does not match its durable start.",
                    null),
                LifecycleExecutionTerminalReason.GenerationMismatch => new CompileLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.GenerationMismatch,
                    "Compile recovery generation was not a proven successor.",
                    null),
                LifecycleExecutionTerminalReason.UnityExited => new CompileLifecycleExecutionError(
                    LifecycleExecutionErrorCodes.UnityExited,
                    "The Unity Editor hosting compile exited before completion.",
                    null),
                _ => new CompileLifecycleExecutionError(
                    UcliCoreErrorCodes.InternalError,
                    "Compile recovery ended with an explicit action failure.",
                    null),
            };
        }

        private CompileLifecycleResult CreatePendingResult (
            UnityEditorRuntimeObservation beforeSnapshot,
            DateTimeOffset startedAtUtc)
        {
            return new CompileLifecycleResult(
                new CompileLifecycleResult.RefreshEvidence(
                    CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
                    Requested: false,
                    startedAtUtc,
                    CompletedAtUtc: null,
                    Completed: false),
                new CompileLifecycleResult.ScriptCompilationEvidence(
                    Started: false,
                    Completed: false,
                    beforeSnapshot.State.Generations.CompileGeneration,
                    beforeSnapshot.State.Generations.CompileGeneration,
                    new CompileLifecycleResult.DiagnosticsEvidence(0, 0, null)),
                new CompileLifecycleResult.DomainReloadEvidence(
                    ReloadRequired: false,
                    ReloadObserved: false,
                    beforeSnapshot.State.Generations.DomainReloadGeneration,
                    beforeSnapshot.State.Generations.DomainReloadGeneration,
                    Settled: false),
                CreateLifecycleEvidence(beforeSnapshot));
        }

        private CompileLifecycleResult CreateFinalResult (
            CompileLifecycleResult pendingResult,
            UnityEditorRuntimeObservation afterSnapshot,
            CompileLifecycleDiagnosticsCheckpoint diagnostics,
            DateTimeOffset refreshCompletedAtUtc)
        {
            var primaryDiagnostic =
                diagnostics.PrimaryDiagnostic ?? afterSnapshot.PrimaryDiagnostic;
            var errorCount = diagnostics.ErrorCount;
            if (errorCount == 0 && primaryDiagnostic != null)
            {
                errorCount = 1;
            }

            var domainReloadObserved =
                pendingResult.DomainReload.GenerationBefore
                    != afterSnapshot.State.Generations.DomainReloadGeneration;
            return new CompileLifecycleResult(
                new CompileLifecycleResult.RefreshEvidence(
                    pendingResult.Refresh.Origin,
                    pendingResult.Refresh.Requested,
                    pendingResult.Refresh.StartedAtUtc,
                    refreshCompletedAtUtc,
                    Completed: true),
                new CompileLifecycleResult.ScriptCompilationEvidence(
                    diagnostics.Started
                        || pendingResult.ScriptCompilation.CompileGenerationBefore
                            != afterSnapshot.State.Generations.CompileGeneration,
                    Completed: true,
                    pendingResult.ScriptCompilation.CompileGenerationBefore,
                    afterSnapshot.State.Generations.CompileGeneration,
                    new CompileLifecycleResult.DiagnosticsEvidence(
                        errorCount,
                        diagnostics.WarningCount,
                        primaryDiagnostic)),
                new CompileLifecycleResult.DomainReloadEvidence(
                    ReloadRequired: domainReloadObserved,
                    ReloadObserved: domainReloadObserved,
                    pendingResult.DomainReload.GenerationBefore,
                    afterSnapshot.State.Generations.DomainReloadGeneration,
                    Settled: IsLifecycleSettled(afterSnapshot)),
                CreateLifecycleEvidence(afterSnapshot));
        }

        private CompileLifecycleResult CreateObservedPartialResult (
            CompileLifecycleExecutionCheckpoint checkpoint,
            UnityEditorRuntimeObservation snapshot)
        {
            if (!LifecycleExecutionGenerationRules.IsMonotonicSuccessor(
                    checkpoint.Before.State.Generations,
                    snapshot.State.Generations))
            {
                return checkpoint.CurrentResult;
            }

            var pendingResult = checkpoint.CurrentResult;
            var diagnostics = checkpoint.Diagnostics;
            var primaryDiagnostic =
                diagnostics.PrimaryDiagnostic ?? snapshot.PrimaryDiagnostic;
            var errorCount = diagnostics.ErrorCount;
            if (errorCount == 0 && primaryDiagnostic != null)
            {
                errorCount = 1;
            }

            var compileGenerationAfter =
                snapshot.State.Generations.CompileGeneration;
            var compileGenerationAdvanced =
                pendingResult.ScriptCompilation.CompileGenerationBefore
                    != compileGenerationAfter;
            var domainReloadGenerationAfter =
                snapshot.State.Generations.DomainReloadGeneration;
            var domainReloadObserved =
                pendingResult.DomainReload.GenerationBefore
                    != domainReloadGenerationAfter;
            return new CompileLifecycleResult(
                new CompileLifecycleResult.RefreshEvidence(
                    pendingResult.Refresh.Origin,
                    pendingResult.Refresh.Requested,
                    pendingResult.Refresh.StartedAtUtc,
                    checkpoint.ProviderReturnedAtUtc,
                    checkpoint.ProviderReturnedAtUtc.HasValue),
                new CompileLifecycleResult.ScriptCompilationEvidence(
                    diagnostics.Started || compileGenerationAdvanced,
                    diagnostics.Completed,
                    pendingResult.ScriptCompilation.CompileGenerationBefore,
                    compileGenerationAfter,
                    new CompileLifecycleResult.DiagnosticsEvidence(
                        errorCount,
                        diagnostics.WarningCount,
                        primaryDiagnostic)),
                new CompileLifecycleResult.DomainReloadEvidence(
                    ReloadRequired: domainReloadObserved,
                    ReloadObserved: domainReloadObserved,
                    pendingResult.DomainReload.GenerationBefore,
                    domainReloadGenerationAfter,
                    Settled: IsLifecycleSettled(snapshot)),
                CreateLifecycleEvidence(snapshot));
        }

        private static bool HasObservedCompileResult (
            CompileLifecycleExecutionCheckpoint checkpoint,
            UnityEditorRuntimeObservation snapshot)
        {
            if (checkpoint?.CurrentResult?.Refresh?.Requested != true)
            {
                return false;
            }
            if (checkpoint.ProviderReturnedAtUtc.HasValue
                || checkpoint.Diagnostics.Started)
            {
                return true;
            }

            var beforeGeneration = checkpoint.Before?.State?.Generations;
            var observedGeneration = snapshot?.State?.Generations;
            return beforeGeneration != null
                && observedGeneration != null
                && LifecycleExecutionGenerationRules.IsMonotonicSuccessor(
                    beforeGeneration,
                    observedGeneration)
                && (observedGeneration.AssetRefreshGeneration
                        > beforeGeneration.AssetRefreshGeneration
                    || observedGeneration.CompileGeneration
                        > beforeGeneration.CompileGeneration
                    || observedGeneration.DomainReloadGeneration
                        > beforeGeneration.DomainReloadGeneration);
        }

        private static void EnsureDiagnosticsAreComplete (
            CompileLifecycleResult pendingResult,
            UnityEditorRuntimeObservation afterSnapshot,
            CompileLifecycleDiagnosticsCheckpoint diagnostics)
        {
            if (diagnostics == null)
            {
                throw new IOException(
                    "Compile diagnostics checkpoint is missing.");
            }

            var compileGenerationAdvanced =
                pendingResult.ScriptCompilation.CompileGenerationBefore
                    != afterSnapshot.State.Generations.CompileGeneration;
            if (!diagnostics.Completed)
            {
                throw new IOException(
                    diagnostics.Started || compileGenerationAdvanced
                        ? "Compile completion cannot be published before durable compilation-finished diagnostics."
                        : "Compile completion cannot be published before durable no-compilation completion.");
            }
        }

        private CompileLifecycleResult.LifecycleEvidence CreateLifecycleEvidence (
            UnityEditorRuntimeObservation snapshot)
        {
            return provider.CreateLifecycleEvidence(snapshot);
        }

        private UnityEditorObservation CreateObservation (
            UnityEditorRuntimeObservation snapshot)
        {
            return provider.CreateLifecycleObservation(snapshot);
        }

        private async Task<UnityEditorRuntimeObservation> WaitUntilCompileSettledAsync (
            Guid executionId,
            ICompileLifecycleExecutionProvider provider,
            CancellationToken cancellationToken)
        {
            var observationWindow = new SettledLifecycleObservationWindow();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = provider.CaptureObservation();
                var checkpoint = await checkpointStore.ReadAsync(
                    executionId,
                    cancellationToken);
                if (checkpoint?.ProviderReturnedAtUtc.HasValue == true)
                {
                    await TryMarkCompilingStateFromEvidenceAsync(
                        checkpoint,
                        snapshot,
                        cancellationToken);
                }
                if (observationWindow.Observe(snapshot))
                {
                    return snapshot;
                }

                await provider.WaitForNextUpdateAsync(
                    cancellationToken);
            }
        }

        private static bool HasCompilationEvidence (
            CompileLifecycleExecutionCheckpoint checkpoint,
            UnityEditorRuntimeObservation observation)
        {
            if (checkpoint == null || observation == null)
            {
                return false;
            }

            return checkpoint.Diagnostics.Started
                || observation.State.Generations.CompileGeneration
                    > checkpoint.CurrentResult.ScriptCompilation
                        .CompileGenerationBefore
                || observation.State.CompileState
                    == UnityEditorCompileState.Compiling
                || observation.State.LifecycleState
                    == UnityEditorLifecycleState.Compiling;
        }

        private static async Task<UnityEditorRuntimeObservation>
            WaitUntilRecoveredCompileSettledAsync (
                ICompileLifecycleExecutionProvider provider,
                CompileLifecycleExecutionCheckpoint checkpoint,
                CancellationToken cancellationToken)
        {
            var startedGeneration = checkpoint.Before.State.Generations;
            var observationWindow = new SettledLifecycleObservationWindow();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = provider.CaptureObservation();
                var generations = snapshot.State.Generations;
                if (observationWindow.Observe(snapshot)
                    && (checkpoint.ProviderReturnedAtUtc.HasValue
                        || !LifecycleExecutionGenerationRules.IsMonotonicSuccessor(
                            startedGeneration,
                            generations)
                        || generations.AssetRefreshGeneration
                            > startedGeneration.AssetRefreshGeneration
                        || generations.CompileGeneration
                            > startedGeneration.CompileGeneration
                        || generations.DomainReloadGeneration
                            > startedGeneration.DomainReloadGeneration))
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
                || IsActionState(
                    executionReference,
                    LifecycleExecutionState.Compiling)
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

        private CompileLifecycleExecutionOutcome CreateErrorResponse (
            UcliCode code,
            string message,
            ExecutionRef lifecycleExecutionRef,
            ExecutionApplicationState applicationState,
            CompileLifecycleResult result,
            string instancePath = null,
            bool hasActionPayload = true)
        {
            return CompileLifecycleExecutionOutcome.Failed(
                code,
                message,
                lifecycleExecutionRef,
                applicationState,
                result,
                CreateObservation(provider.CaptureObservation()),
                instancePath,
                hasActionPayload);
        }

        private sealed record TerminalCandidate (
            Guid ExecutionId,
            CompileLifecycleResult Result,
            LifecycleExecutionTerminalReason TerminalReason,
            ExecutionApplicationState ApplicationState,
            UnityEditorGenerationSnapshot TerminalGeneration,
            DateTimeOffset CompletedAtUtc,
            Verdict? Verdict);

        private sealed class SettledLifecycleObservationWindow
        {
            private int stableUpdates;
            private bool hasStableSnapshot;
            private UnityEditorLifecycleState stableLifecycleState;
            private UnityEditorCompileState stableCompileState;
            private long stableCompileGeneration;
            private long stableDomainReloadGeneration;

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

            private bool MatchesStableSnapshot (
                UnityEditorRuntimeObservation snapshot)
            {
                return stableLifecycleState == snapshot.State.LifecycleState
                    && stableCompileState == snapshot.State.CompileState
                    && stableCompileGeneration
                        == snapshot.State.Generations.CompileGeneration
                    && stableDomainReloadGeneration
                        == snapshot.State.Generations.DomainReloadGeneration;
            }

            private void CaptureStableSnapshot (
                UnityEditorRuntimeObservation snapshot)
            {
                hasStableSnapshot = true;
                stableLifecycleState = snapshot.State.LifecycleState;
                stableCompileState = snapshot.State.CompileState;
                stableCompileGeneration =
                    snapshot.State.Generations.CompileGeneration;
                stableDomainReloadGeneration =
                    snapshot.State.Generations.DomainReloadGeneration;
            }

            private void Reset ()
            {
                stableUpdates = 0;
                hasStableSnapshot = false;
                stableLifecycleState = default;
                stableCompileState = default;
                stableCompileGeneration = default;
                stableDomainReloadGeneration = default;
            }
        }

    }
}
