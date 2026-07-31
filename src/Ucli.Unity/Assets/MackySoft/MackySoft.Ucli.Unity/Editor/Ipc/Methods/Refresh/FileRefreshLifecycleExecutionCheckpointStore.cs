using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists checkpoints interpreted only by the refresh handler. </summary>
    internal sealed class FileRefreshLifecycleExecutionCheckpointStore :
        ILifecycleExecutionSideEffectAdmissionCheckpointStore<
            RefreshLifecycleExecutionCheckpoint>
    {
        private const string CheckpointFileName = "refresh-checkpoint.json";

        private readonly LifecycleExecutionActionCheckpointPersistence<
            RefreshLifecycleExecutionCheckpoint> persistence;

        public FileRefreshLifecycleExecutionCheckpointStore (
            FileLifecycleExecutionStore executionStore)
        {
            persistence = new LifecycleExecutionActionCheckpointPersistence<
                RefreshLifecycleExecutionCheckpoint>(
                executionStore,
                LifecycleExecutionKind.Refresh,
                CheckpointFileName,
                static checkpoint => checkpoint.ExecutionId);
        }

        public ValueTask<RefreshLifecycleExecutionCheckpoint> ReadAsync (
            Guid executionId,
            CancellationToken cancellationToken)
        {
            return persistence.ReadAsync(executionId, cancellationToken);
        }

        public bool IsAdmitted (
            RefreshLifecycleExecutionCheckpoint checkpoint)
        {
            return checkpoint?.SideEffectAdmitted == true;
        }

        public ValueTask<RefreshLifecycleExecutionCheckpoint> WritePreparedAsync (
            Guid executionId,
            UnityEditorObservation before,
            CancellationToken cancellationToken)
        {
            return persistence.MutateAsync(
                executionId,
                existing => existing
                    ?? new RefreshLifecycleExecutionCheckpoint(
                        RefreshLifecycleExecutionCheckpoint
                            .CurrentSchemaVersion,
                        executionId,
                        before,
                        dispatchCandidate: null,
                        sideEffectAdmitted: false,
                        providerInvocationObserved: false,
                        providerReturned: false),
                cancellationToken);
        }

        public ValueTask<RefreshLifecycleExecutionCheckpoint> MarkAdmittedAsync (
            RefreshLifecycleExecutionCheckpoint checkpoint,
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
                            "Refresh checkpoint disappeared before side-effect admission.");
                    }
                    if (current.SideEffectAdmitted)
                    {
                        return current;
                    }

                    return new RefreshLifecycleExecutionCheckpoint(
                        RefreshLifecycleExecutionCheckpoint
                            .CurrentSchemaVersion,
                        current.ExecutionId,
                        current.Before,
                        current.DispatchCandidate,
                        sideEffectAdmitted: true,
                        current.ProviderInvocationObserved,
                        current.ProviderReturned);
                },
                cancellationToken);
        }

        public ValueTask<RefreshLifecycleExecutionCheckpoint>
            MarkDispatchPreparedAsync (
                RefreshLifecycleExecutionCheckpoint checkpoint,
                RefreshLifecycleDispatchCandidate dispatchCandidate,
                CancellationToken cancellationToken)
        {
            if (checkpoint == null)
            {
                throw new ArgumentNullException(nameof(checkpoint));
            }
            if (dispatchCandidate == null)
            {
                throw new ArgumentNullException(nameof(dispatchCandidate));
            }

            return persistence.MutateAsync(
                checkpoint.ExecutionId,
                current =>
                {
                    if (current == null)
                    {
                        throw new IOException(
                            "Refresh checkpoint disappeared before provider dispatch preparation.");
                    }
                    if (!current.SideEffectAdmitted)
                    {
                        throw new InvalidOperationException(
                            "Refresh provider dispatch preparation cannot precede side-effect admission.");
                    }
                    if (current.DispatchCandidate != null)
                    {
                        return current;
                    }

                    return new RefreshLifecycleExecutionCheckpoint(
                        RefreshLifecycleExecutionCheckpoint
                            .CurrentSchemaVersion,
                        current.ExecutionId,
                        current.Before,
                        dispatchCandidate,
                        current.SideEffectAdmitted,
                        current.ProviderInvocationObserved,
                        current.ProviderReturned);
                },
                cancellationToken);
        }

        public ValueTask<RefreshLifecycleExecutionCheckpoint>
            MarkProviderInvocationObservedAsync (
                RefreshLifecycleExecutionCheckpoint checkpoint,
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
                            "Refresh checkpoint disappeared after provider invocation.");
                    }
                    if (!current.SideEffectAdmitted
                        || current.DispatchCandidate == null)
                    {
                        throw new InvalidOperationException(
                            "Refresh provider invocation cannot precede durable dispatch preparation.");
                    }
                    if (current.ProviderInvocationObserved)
                    {
                        return current;
                    }

                    return new RefreshLifecycleExecutionCheckpoint(
                        RefreshLifecycleExecutionCheckpoint
                            .CurrentSchemaVersion,
                        current.ExecutionId,
                        current.Before,
                        current.DispatchCandidate,
                        current.SideEffectAdmitted,
                        providerInvocationObserved: true,
                        current.ProviderReturned);
                },
                cancellationToken);
        }

        public ValueTask<RefreshLifecycleExecutionCheckpoint>
            MarkProviderReturnedAsync (
                RefreshLifecycleExecutionCheckpoint checkpoint,
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
                            "Refresh checkpoint disappeared before provider return.");
                    }
                    if (!current.SideEffectAdmitted)
                    {
                        throw new InvalidOperationException(
                            "Refresh provider return cannot precede side-effect admission.");
                    }
                    if (current.DispatchCandidate == null)
                    {
                        throw new InvalidOperationException(
                            "Refresh provider return cannot precede durable dispatch preparation.");
                    }
                    if (current.ProviderReturned)
                    {
                        return current;
                    }

                    return new RefreshLifecycleExecutionCheckpoint(
                        RefreshLifecycleExecutionCheckpoint
                            .CurrentSchemaVersion,
                        current.ExecutionId,
                        current.Before,
                        current.DispatchCandidate,
                        current.SideEffectAdmitted,
                        providerInvocationObserved: true,
                        providerReturned: true);
                },
                cancellationToken);
        }
    }
}
