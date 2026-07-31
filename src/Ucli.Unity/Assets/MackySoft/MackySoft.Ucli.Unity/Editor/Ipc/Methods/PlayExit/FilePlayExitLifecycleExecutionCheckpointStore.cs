using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Persists only the Play Mode exit state machine checkpoint under its common execution identity.
    /// </summary>
    internal sealed class FilePlayExitLifecycleExecutionCheckpointStore :
        ILifecycleExecutionSideEffectAdmissionCheckpointStore<
            PlayExitLifecycleExecutionCheckpoint>
    {
        private const string CheckpointFileName = "play-exit-checkpoint.json";

        private readonly LifecycleExecutionActionCheckpointPersistence<
            PlayExitLifecycleExecutionCheckpoint> persistence;

        public FilePlayExitLifecycleExecutionCheckpointStore (
            FileLifecycleExecutionStore executionStore)
        {
            persistence = new LifecycleExecutionActionCheckpointPersistence<
                PlayExitLifecycleExecutionCheckpoint>(
                executionStore,
                LifecycleExecutionKind.PlayExit,
                CheckpointFileName,
                static checkpoint => checkpoint.ExecutionId);
        }

        public ValueTask<PlayExitLifecycleExecutionCheckpoint> ReadAsync (
            Guid executionId,
            CancellationToken cancellationToken)
        {
            return persistence.ReadAsync(executionId, cancellationToken);
        }

        public bool IsAdmitted (
            PlayExitLifecycleExecutionCheckpoint checkpoint)
        {
            return checkpoint?.SideEffectAdmitted == true;
        }

        public async ValueTask<CreateResult> CreateOrReadAsync (
            Guid executionId,
            UnityEditorObservation before,
            CancellationToken cancellationToken)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            var created = false;
            var checkpoint = await persistence.MutateAsync(
                executionId,
                existing =>
                {
                    if (existing != null)
                    {
                        return existing;
                    }

                    created = true;
                    return new PlayExitLifecycleExecutionCheckpoint(
                        PlayExitLifecycleExecutionCheckpoint
                            .CurrentSchemaVersion,
                        executionId,
                        before,
                        sideEffectAdmitted: false);
                },
                cancellationToken);
            return new CreateResult(created, checkpoint);
        }

        public ValueTask<PlayExitLifecycleExecutionCheckpoint> MarkAdmittedAsync (
            PlayExitLifecycleExecutionCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            if (checkpoint == null)
            {
                throw new ArgumentNullException(nameof(checkpoint));
            }

            return persistence.MutateAsync(
                checkpoint.ExecutionId,
                current =>
                {
                    if (current == null)
                    {
                        throw new IOException(
                            "Play Mode exit checkpoint disappeared before side-effect admission.");
                    }
                    if (current.SideEffectAdmitted)
                    {
                        return current;
                    }

                    return current with { SideEffectAdmitted = true };
                },
                cancellationToken);
        }

        internal sealed record CreateResult (
            bool Created,
            PlayExitLifecycleExecutionCheckpoint Checkpoint);
    }
}
