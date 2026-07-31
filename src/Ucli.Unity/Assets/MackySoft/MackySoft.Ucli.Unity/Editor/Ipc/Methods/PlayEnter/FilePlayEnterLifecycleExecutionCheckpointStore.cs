using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists checkpoints interpreted only by the Play Mode entry handler. </summary>
    internal sealed class FilePlayEnterLifecycleExecutionCheckpointStore :
        ILifecycleExecutionSideEffectAdmissionCheckpointStore<
            PlayEnterLifecycleExecutionCheckpoint>
    {
        private const string CheckpointFileName = "play-enter-checkpoint.json";

        private readonly LifecycleExecutionActionCheckpointPersistence<
            PlayEnterLifecycleExecutionCheckpoint> persistence;

        public FilePlayEnterLifecycleExecutionCheckpointStore (
            FileLifecycleExecutionStore executionStore)
        {
            persistence = new LifecycleExecutionActionCheckpointPersistence<
                PlayEnterLifecycleExecutionCheckpoint>(
                executionStore,
                LifecycleExecutionKind.PlayEnter,
                CheckpointFileName,
                static checkpoint => checkpoint.ExecutionId);
        }

        public ValueTask<PlayEnterLifecycleExecutionCheckpoint> ReadAsync (
            Guid executionId,
            CancellationToken cancellationToken)
        {
            return persistence.ReadAsync(executionId, cancellationToken);
        }

        public bool IsAdmitted (
            PlayEnterLifecycleExecutionCheckpoint checkpoint)
        {
            return checkpoint?.SideEffectAdmitted == true;
        }

        public ValueTask<PlayEnterLifecycleExecutionCheckpoint> WritePreparedAsync (
            Guid executionId,
            UnityEditorObservation before,
            CancellationToken cancellationToken)
        {
            return persistence.MutateAsync(
                executionId,
                existing => existing
                    ?? new PlayEnterLifecycleExecutionCheckpoint(
                        PlayEnterLifecycleExecutionCheckpoint
                            .CurrentSchemaVersion,
                        executionId,
                        before,
                        sideEffectAdmitted: false),
                cancellationToken);
        }

        public ValueTask<PlayEnterLifecycleExecutionCheckpoint> MarkAdmittedAsync (
            PlayEnterLifecycleExecutionCheckpoint checkpoint,
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
                            "Play Mode entry checkpoint disappeared before side-effect admission.");
                    }
                    if (current.SideEffectAdmitted)
                    {
                        return current;
                    }

                    return new PlayEnterLifecycleExecutionCheckpoint(
                        PlayEnterLifecycleExecutionCheckpoint
                            .CurrentSchemaVersion,
                        current.ExecutionId,
                        current.Before,
                        sideEffectAdmitted: true);
                },
                cancellationToken);
        }
    }
}
