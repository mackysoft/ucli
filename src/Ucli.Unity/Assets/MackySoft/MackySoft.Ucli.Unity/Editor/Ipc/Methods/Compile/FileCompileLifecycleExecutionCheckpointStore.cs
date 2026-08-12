using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists checkpoints interpreted only by the compile handler. </summary>
    internal sealed class FileCompileLifecycleExecutionCheckpointStore :
        ILifecycleExecutionSideEffectAdmissionCheckpointStore<
            CompileLifecycleExecutionCheckpoint>
    {
        private const string CheckpointFileName = "compile-checkpoint.json";

        private readonly LifecycleExecutionActionCheckpointPersistence<
            CompileLifecycleExecutionCheckpoint> persistence;

        public FileCompileLifecycleExecutionCheckpointStore (
            FileLifecycleExecutionStore executionStore)
        {
            persistence = new LifecycleExecutionActionCheckpointPersistence<
                CompileLifecycleExecutionCheckpoint>(
                executionStore,
                LifecycleExecutionKind.Compile,
                CheckpointFileName,
                static checkpoint => checkpoint.ExecutionId);
        }

        public ValueTask<CompileLifecycleExecutionCheckpoint> ReadAsync (
            Guid executionId,
            CancellationToken cancellationToken)
        {
            return persistence.ReadAsync(executionId, cancellationToken);
        }

        public bool IsAdmitted (
            CompileLifecycleExecutionCheckpoint checkpoint)
        {
            return checkpoint?.SideEffectAdmitted == true;
        }

        public ValueTask<CompileLifecycleExecutionCheckpoint> WritePreparedAsync (
            Guid executionId,
            UnityEditorObservation before,
            CompileLifecycleResult pendingResult,
            CancellationToken cancellationToken)
        {
            return persistence.MutateAsync(
                executionId,
                existing => existing
                    ?? new CompileLifecycleExecutionCheckpoint(
                        CompileLifecycleExecutionCheckpoint
                            .CurrentSchemaVersion,
                        executionId,
                        before,
                        sideEffectAdmitted: false,
                        providerReturnedAtUtc: null,
                        pendingResult,
                        CompileLifecycleDiagnosticsCheckpoint.Empty),
                cancellationToken);
        }

        public ValueTask<CompileLifecycleExecutionCheckpoint> MarkAdmittedAsync (
            CompileLifecycleExecutionCheckpoint checkpoint,
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
                            "Compile checkpoint disappeared before side-effect admission.");
                    }
                    if (current.SideEffectAdmitted)
                    {
                        return current;
                    }

                    return Copy(
                        current,
                        sideEffectAdmitted: true);
                },
                cancellationToken);
        }

        public ValueTask<CompileLifecycleExecutionCheckpoint>
            MarkDispatchPreparedAsync (
                CompileLifecycleExecutionCheckpoint checkpoint,
                DateTimeOffset startedAtUtc,
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
                            "Compile checkpoint disappeared before refresh dispatch preparation.");
                    }
                    if (!current.SideEffectAdmitted)
                    {
                        throw new InvalidOperationException(
                            "Compile refresh dispatch preparation cannot precede side-effect admission.");
                    }
                    if (current.CurrentResult.Refresh.Requested)
                    {
                        return current;
                    }

                    var result = new CompileLifecycleResult(
                        new CompileLifecycleResult.RefreshEvidence(
                            current.CurrentResult.Refresh.Origin,
                            Requested: true,
                            startedAtUtc,
                            CompletedAtUtc: null,
                            Completed: false),
                        current.CurrentResult.ScriptCompilation,
                        current.CurrentResult.DomainReload,
                        current.CurrentResult.Lifecycle);
                    return Copy(current, currentResult: result);
                },
                cancellationToken);
        }

        public ValueTask<CompileLifecycleExecutionCheckpoint>
            MarkProviderReturnedAsync (
            CompileLifecycleExecutionCheckpoint checkpoint,
            DateTimeOffset providerReturnedAtUtc,
            CancellationToken cancellationToken)
        {
            if (checkpoint == null)
            {
                throw new ArgumentNullException(nameof(checkpoint));
            }
            if (providerReturnedAtUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Compile provider return time must use the UTC offset.",
                    nameof(providerReturnedAtUtc));
            }

            return persistence.MutateAsync(
                checkpoint.ExecutionId,
                current =>
                {
                    if (current == null)
                    {
                        throw new IOException(
                            "Compile checkpoint disappeared before provider return.");
                    }
                    if (!current.SideEffectAdmitted)
                    {
                        throw new InvalidOperationException(
                            "Compile refresh provider return cannot precede side-effect admission.");
                    }
                    if (!current.CurrentResult.Refresh.Requested)
                    {
                        throw new InvalidOperationException(
                            "Compile refresh provider return cannot precede durable dispatch preparation.");
                    }
                    if (current.ProviderReturnedAtUtc.HasValue)
                    {
                        return current;
                    }

                    return Copy(
                        current,
                        providerReturnedAtUtc: providerReturnedAtUtc);
                },
                cancellationToken);
        }

        public ICompileLifecycleExecutionDiagnosticsSink
            CreateDiagnosticsSink (Guid executionId)
        {
            if (executionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Compile execution identifier must not be empty.",
                    nameof(executionId));
            }

            return new DiagnosticsSink(this, executionId);
        }

        public ValueTask<CompileLifecycleExecutionCheckpoint>
            MarkNoCompilationRequiredAsync (
            Guid executionId,
            CancellationToken cancellationToken)
        {
            return persistence.MutateAsync(
                executionId,
                current =>
                {
                    if (current == null)
                    {
                        throw new IOException(
                            "Compile checkpoint disappeared before no-compilation completion.");
                    }
                    if (current.Diagnostics.Completed)
                    {
                        return current;
                    }
                    if (current.Diagnostics.Started)
                    {
                        throw new IOException(
                            "Started compile diagnostics cannot complete without their matching batch-finished callback.");
                    }

                    return Copy(
                        current,
                        diagnostics:
                            CompileLifecycleDiagnosticsCheckpoint
                                .NoCompilationRequired);
                },
                cancellationToken);
        }

        private long? GetActiveBatchId (Guid executionId)
        {
            return persistence.Read(executionId)?.Diagnostics.ActiveBatchId;
        }

        private long? GetLastProcessedBatchId (Guid executionId)
        {
            var batchIds =
                persistence.Read(executionId)?.Diagnostics.ProcessedBatchIds;
            return batchIds == null || batchIds.Count == 0
                ? null
                : batchIds[batchIds.Count - 1];
        }

        private void StartBatch (
            Guid executionId,
            long batchId)
        {
            if (batchId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(batchId),
                    batchId,
                    "Compile batch identifier must not be negative.");
            }

            _ = persistence.Mutate(
                executionId,
                current =>
                {
                    if (current == null)
                    {
                        throw new IOException(
                            "Compile checkpoint disappeared before compilation started.");
                    }
                    if (current.Diagnostics.ActiveBatchId == batchId
                        || Contains(
                            current.Diagnostics.ProcessedBatchIds,
                            batchId))
                    {
                        return current;
                    }
                    if (current.Diagnostics.ActiveBatchId.HasValue)
                    {
                        throw new IOException(
                            "A different compile batch is already active.");
                    }

                    var diagnostics =
                        new CompileLifecycleDiagnosticsCheckpoint(
                            started: true,
                            completed: false,
                            activeBatchId: batchId,
                            processedBatchIds:
                                current.Diagnostics.ProcessedBatchIds,
                            processedAssemblies:
                                current.Diagnostics.ProcessedAssemblies);
                    return Copy(current, diagnostics: diagnostics);
                });
        }

        private void RecordAssembly (
            Guid executionId,
            long batchId,
            string assemblyIdentity,
            int errorCount,
            int warningCount,
            UnityEditorPrimaryDiagnostic primaryDiagnostic)
        {
            var assembly =
                new CompileLifecycleDiagnosticsAssemblyCheckpoint(
                    batchId,
                    assemblyIdentity,
                errorCount,
                warningCount,
                primaryDiagnostic);
            _ = persistence.Mutate(
                executionId,
                current =>
                {
                    if (current == null)
                    {
                        throw new IOException(
                            "Compile checkpoint disappeared before assembly diagnostics.");
                    }
                    for (var index = 0;
                         index
                            < current.Diagnostics.ProcessedAssemblies.Count;
                         index++)
                    {
                        var existing =
                            current.Diagnostics.ProcessedAssemblies[index];
                        if (existing.BatchId != batchId
                            || !string.Equals(
                                existing.AssemblyIdentity,
                                assemblyIdentity,
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (existing != assembly)
                        {
                            throw new IOException(
                                "Repeated compile assembly callback conflicts with its durable diagnostic summary.");
                        }

                        return current;
                    }

                    if (current.Diagnostics.ActiveBatchId != batchId)
                    {
                        throw new IOException(
                            "Compile assembly callback does not belong to the active batch.");
                    }

                    var assemblies =
                        new CompileLifecycleDiagnosticsAssemblyCheckpoint[
                            current.Diagnostics.ProcessedAssemblies.Count + 1];
                    for (var index = 0;
                         index
                            < current.Diagnostics.ProcessedAssemblies.Count;
                         index++)
                    {
                        assemblies[index] =
                            current.Diagnostics.ProcessedAssemblies[index];
                    }
                    assemblies[assemblies.Length - 1] = assembly;
                    var diagnostics =
                        new CompileLifecycleDiagnosticsCheckpoint(
                            started: true,
                            completed: false,
                            activeBatchId: batchId,
                            processedBatchIds:
                                current.Diagnostics.ProcessedBatchIds,
                            processedAssemblies: assemblies);
                    return Copy(current, diagnostics: diagnostics);
                });
        }

        private void CompleteBatch (
            Guid executionId,
            long batchId)
        {
            _ = persistence.Mutate(
                executionId,
                current =>
                {
                    if (current == null)
                    {
                        throw new IOException(
                            "Compile checkpoint disappeared before compilation completed.");
                    }
                    if (Contains(
                            current.Diagnostics.ProcessedBatchIds,
                            batchId))
                    {
                        return current;
                    }
                    if (current.Diagnostics.ActiveBatchId != batchId)
                    {
                        throw new IOException(
                            "Compile completion callback does not match the active batch.");
                    }

                    var batchIds = new long[
                        current.Diagnostics.ProcessedBatchIds.Count + 1];
                    for (var index = 0;
                         index < current.Diagnostics.ProcessedBatchIds.Count;
                         index++)
                    {
                        batchIds[index] =
                            current.Diagnostics.ProcessedBatchIds[index];
                    }
                    batchIds[batchIds.Length - 1] = batchId;
                    var diagnostics =
                        new CompileLifecycleDiagnosticsCheckpoint(
                            started: true,
                            completed: true,
                            activeBatchId: null,
                            processedBatchIds: batchIds,
                            processedAssemblies:
                                current.Diagnostics.ProcessedAssemblies);
                    return Copy(current, diagnostics: diagnostics);
                });
        }

        private static bool Contains (
            System.Collections.Generic.IReadOnlyList<long> values,
            long value)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (values[index] == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static CompileLifecycleExecutionCheckpoint Copy (
            CompileLifecycleExecutionCheckpoint checkpoint,
            bool? sideEffectAdmitted = null,
            DateTimeOffset? providerReturnedAtUtc = null,
            CompileLifecycleResult currentResult = null,
            CompileLifecycleDiagnosticsCheckpoint diagnostics = null)
        {
            return new CompileLifecycleExecutionCheckpoint(
                CompileLifecycleExecutionCheckpoint.CurrentSchemaVersion,
                checkpoint.ExecutionId,
                checkpoint.Before,
                sideEffectAdmitted ?? checkpoint.SideEffectAdmitted,
                providerReturnedAtUtc ?? checkpoint.ProviderReturnedAtUtc,
                currentResult ?? checkpoint.CurrentResult,
                diagnostics ?? checkpoint.Diagnostics);
        }

        private sealed class DiagnosticsSink :
            ICompileLifecycleExecutionDiagnosticsSink
        {
            private readonly FileCompileLifecycleExecutionCheckpointStore owner;

            private readonly Guid executionId;

            public DiagnosticsSink (
                FileCompileLifecycleExecutionCheckpointStore owner,
                Guid executionId)
            {
                this.owner = owner;
                this.executionId = executionId;
            }

            public long? ActiveBatchId =>
                owner.GetActiveBatchId(executionId);

            public long? LastProcessedBatchId =>
                owner.GetLastProcessedBatchId(executionId);

            public void StartBatch (long batchId)
            {
                owner.StartBatch(executionId, batchId);
            }

            public void RecordAssembly (
                long batchId,
                string assemblyIdentity,
                int errorCount,
                int warningCount,
                UnityEditorPrimaryDiagnostic primaryDiagnostic)
            {
                owner.RecordAssembly(
                    executionId,
                    batchId,
                    assemblyIdentity,
                    errorCount,
                    warningCount,
                    primaryDiagnostic);
            }

            public void CompleteBatch (long batchId)
            {
                owner.CompleteBatch(executionId, batchId);
            }
        }
    }
}
