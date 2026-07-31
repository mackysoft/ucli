using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Owns the Play Mode entry checkpoint and its one-way side-effect admission.
    /// </summary>
    internal sealed class PlayEnterLifecycleExecutionContext :
        IPlayEnterLifecycleExecutionContext
    {
        private readonly FileLifecycleExecutionStore executionStore;
        private readonly FilePlayEnterLifecycleExecutionCheckpointStore checkpointStore;
        private readonly LifecycleExecutionSideEffectAdmissionCoordinator
            sideEffectAdmission;
        private readonly Guid executionId;
        private readonly Guid claimantEndpointRegistrationGenerationId;
        private readonly bool enterRecoveryWhenReconnecting;
        private PlayEnterLifecycleExecutionCheckpoint checkpoint;

        public PlayEnterLifecycleExecutionContext (
            FileLifecycleExecutionStore executionStore,
            FilePlayEnterLifecycleExecutionCheckpointStore checkpointStore,
            Guid executionId,
            Guid claimantEndpointRegistrationGenerationId,
            PlayEnterLifecycleExecutionCheckpoint checkpoint,
            bool enterRecoveryWhenReconnecting)
        {
            this.executionStore = executionStore
                ?? throw new ArgumentNullException(nameof(executionStore));
            this.checkpointStore = checkpointStore
                ?? throw new ArgumentNullException(nameof(checkpointStore));
            sideEffectAdmission =
                new LifecycleExecutionSideEffectAdmissionCoordinator(
                    this.executionStore);
            if (executionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Play Mode entry execution identifier must not be empty.",
                    nameof(executionId));
            }

            this.executionId = executionId;
            if (claimantEndpointRegistrationGenerationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Play Mode entry claimant endpoint registration generation must not be empty.",
                    nameof(claimantEndpointRegistrationGenerationId));
            }

            this.claimantEndpointRegistrationGenerationId =
                claimantEndpointRegistrationGenerationId;
            this.checkpoint = checkpoint;
            this.enterRecoveryWhenReconnecting =
                enterRecoveryWhenReconnecting;
        }

        public bool HasSideEffectAdmission =>
            checkpoint?.SideEffectAdmitted == true;

        public bool TryReadBefore (
            out UnityEditorObservation before,
            out string errorMessage)
        {
            before = checkpoint?.Before;
            if (before == null)
            {
                errorMessage =
                    "Durable Play Mode entry before snapshot is missing.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public async ValueTask<bool> TryAdmitSideEffectAsync (
            UnityEditorObservation before,
            CancellationToken cancellationToken)
        {
            var stored = await executionStore.ReadAsync(
                LifecycleExecutionKind.PlayEnter,
                executionId,
                cancellationToken);
            if (stored == null)
            {
                throw new InvalidOperationException(
                    "Play Mode entry execution is missing.");
            }

            var currentReference = stored.CurrentReference;
            if (stored.IsTerminal || stored.IsPublishing)
            {
                checkpoint = await checkpointStore.ReadAsync(
                    executionId,
                    cancellationToken);
                return false;
            }

            if (!IsRegistered(currentReference))
            {
                checkpoint ??= await checkpointStore.ReadAsync(
                    executionId,
                    cancellationToken)
                    ?? throw new InvalidOperationException(
                        "Play Mode entry checkpoint disappeared during side-effect reconnection.");
                var reconnected = await sideEffectAdmission.ReconnectAsync(
                    LifecycleExecutionKind.PlayEnter,
                    stored,
                    claimantEndpointRegistrationGenerationId,
                    checkpointStore,
                    checkpoint,
                    cancellationToken);
                EnsureRecoverableAdmission(reconnected);
                checkpoint = reconnected.Checkpoint;
                if (enterRecoveryWhenReconnecting
                    && reconnected.State
                        == LifecycleExecutionSideEffectAdmissionCoordinator
                            .Outcome.Recover)
                {
                    _ = await TryEnterRecoveryAfterAdmissionAsync(
                        cancellationToken);
                }

                return reconnected.State
                    == LifecycleExecutionSideEffectAdmissionCoordinator.Outcome
                        .Acquired;
            }

            checkpoint = await checkpointStore.WritePreparedAsync(
                executionId,
                before,
                cancellationToken);

            var enteringReference =
                LifecycleExecutionReferenceFactory.CreateStateProjection(
                    currentReference,
                    ExecutionLifecycle.Active,
                    LifecycleExecutionState.Entering);
            var admission = await sideEffectAdmission.AcquireAsync(
                    LifecycleExecutionKind.PlayEnter,
                    stored,
                    enteringReference,
                    claimantEndpointRegistrationGenerationId,
                    checkpointStore,
                    checkpoint,
                    cancellationToken);
            EnsureRecoverableAdmission(admission);
            checkpoint = admission.Checkpoint;
            if (enterRecoveryWhenReconnecting
                && admission.State
                    == LifecycleExecutionSideEffectAdmissionCoordinator
                        .Outcome.Recover)
            {
                _ = await TryEnterRecoveryAfterAdmissionAsync(
                    cancellationToken);
            }

            return admission.State
                == LifecycleExecutionSideEffectAdmissionCoordinator.Outcome
                    .Acquired;
        }

        public async ValueTask<bool> TryEnterRecoveryAfterAdmissionAsync (
            CancellationToken cancellationToken)
        {
            if (!HasSideEffectAdmission)
            {
                throw new InvalidOperationException(
                    "Play Mode entry recovery requires its durable side-effect admission marker.");
            }

            var outcome = await executionStore.TryEnterRecoveryAsync(
                LifecycleExecutionKind.PlayEnter,
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
                        "Play Mode entry recovery cannot precede its side-effect admission."),
                LifecycleExecutionRecoveryTransitionOutcome.Missing =>
                    throw new InvalidOperationException(
                        "Play Mode entry execution disappeared while entering recovery."),
                _ => throw new InvalidOperationException(
                    $"Play Mode entry recovery transition could not classify outcome '{outcome}'."),
            };
        }

        private static void EnsureRecoverableAdmission (
            LifecycleExecutionSideEffectAdmissionCoordinator.Resolution<
                PlayEnterLifecycleExecutionCheckpoint> admission)
        {
            if (admission.State
                    != LifecycleExecutionSideEffectAdmissionCoordinator.Outcome
                        .Recover
                || IsEntering(
                    admission.AuthoritativeExecution.CurrentReference)
                || IsRecovering(
                    admission.AuthoritativeExecution.CurrentReference))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Play Mode entry execution state '{admission.AuthoritativeExecution.CurrentReference.State.Value}' cannot recover its side effect.");
        }

        private static bool IsRegistered (
            ExecutionRef executionReference)
        {
            return executionReference.Lifecycle == ExecutionLifecycle.Active
                && string.Equals(
                    executionReference.State.Value,
                    TextVocabulary.GetText(LifecycleExecutionState.Registered),
                    StringComparison.Ordinal);
        }

        private static bool IsEntering (ExecutionRef executionReference)
        {
            return executionReference.Lifecycle == ExecutionLifecycle.Active
                && string.Equals(
                    executionReference.State.Value,
                    TextVocabulary.GetText(LifecycleExecutionState.Entering),
                    StringComparison.Ordinal);
        }

        private static bool IsRecovering (ExecutionRef executionReference)
        {
            return executionReference.Lifecycle == ExecutionLifecycle.Recovery
                && string.Equals(
                    executionReference.State.Value,
                    TextVocabulary.GetText(LifecycleExecutionState.Recovering),
                    StringComparison.Ordinal);
        }

    }
}
