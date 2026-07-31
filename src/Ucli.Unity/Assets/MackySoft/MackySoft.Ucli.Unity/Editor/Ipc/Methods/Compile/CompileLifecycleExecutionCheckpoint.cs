using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Retains compile evidence interpreted only by the compile handler. </summary>
    internal sealed record CompileLifecycleExecutionCheckpoint
    {
        public const int CurrentSchemaVersion = 2;

        [JsonConstructor]
        public CompileLifecycleExecutionCheckpoint (
            int schemaVersion,
            Guid executionId,
            UnityEditorObservation before,
            bool sideEffectAdmitted,
            DateTimeOffset? providerReturnedAtUtc,
            CompileLifecycleResult currentResult,
            CompileLifecycleDiagnosticsCheckpoint diagnostics)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    schemaVersion,
                    "Unsupported compile checkpoint schema version.");
            }
            if (executionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Compile execution identifier must not be empty.",
                    nameof(executionId));
            }

            SchemaVersion = schemaVersion;
            ExecutionId = executionId;
            Before = before;
            SideEffectAdmitted = sideEffectAdmitted;
            CurrentResult = currentResult;
            Diagnostics = diagnostics
                ?? throw new ArgumentNullException(nameof(diagnostics));
            if (sideEffectAdmitted && (before == null || currentResult == null))
            {
                throw new ArgumentException(
                    "An admitted compile side effect requires its durable before evidence.",
                    nameof(sideEffectAdmitted));
            }
            if (providerReturnedAtUtc.HasValue && !sideEffectAdmitted)
            {
                throw new ArgumentException(
                    "A returned compile refresh provider call requires prior side-effect admission.",
                    nameof(providerReturnedAtUtc));
            }
            if (providerReturnedAtUtc.HasValue)
            {
                if (!currentResult.Refresh.Requested)
                {
                    throw new ArgumentException(
                        "A returned compile refresh provider call requires durable dispatch preparation.",
                        nameof(providerReturnedAtUtc));
                }
                if (providerReturnedAtUtc.Value.Offset != TimeSpan.Zero)
                {
                    throw new ArgumentException(
                        "Compile refresh provider return time must use the UTC offset.",
                        nameof(providerReturnedAtUtc));
                }
                if (providerReturnedAtUtc.Value
                    < currentResult.Refresh.StartedAtUtc)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(providerReturnedAtUtc),
                        providerReturnedAtUtc,
                        "Compile refresh provider return time must not precede refresh start.");
                }
            }
            ProviderReturnedAtUtc = providerReturnedAtUtc;
        }

        public int SchemaVersion { get; }

        public Guid ExecutionId { get; }

        public UnityEditorObservation Before { get; }

        public bool SideEffectAdmitted { get; }

        public DateTimeOffset? ProviderReturnedAtUtc { get; }

        public CompileLifecycleResult CurrentResult { get; }

        public CompileLifecycleDiagnosticsCheckpoint Diagnostics { get; }
    }

    /// <summary>
    /// Retains callback evidence emitted before Unity replaces the current
    /// application domain.
    /// </summary>
    internal sealed record CompileLifecycleDiagnosticsCheckpoint
    {
        [JsonConstructor]
        public CompileLifecycleDiagnosticsCheckpoint (
            bool started,
            bool completed,
            long? activeBatchId,
            IReadOnlyList<long> processedBatchIds,
            IReadOnlyList<CompileLifecycleDiagnosticsAssemblyCheckpoint>
                processedAssemblies)
        {
            if (activeBatchId is < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(activeBatchId),
                    activeBatchId,
                    "Active compile batch identifier must not be negative.");
            }
            if (processedBatchIds == null)
            {
                throw new ArgumentNullException(nameof(processedBatchIds));
            }
            if (processedAssemblies == null)
            {
                throw new ArgumentNullException(nameof(processedAssemblies));
            }

            var batchIds = new long[processedBatchIds.Count];
            var uniqueBatchIds = new HashSet<long>();
            for (var index = 0; index < processedBatchIds.Count; index++)
            {
                var batchId = processedBatchIds[index];
                if (batchId < 0 || !uniqueBatchIds.Add(batchId))
                {
                    throw new ArgumentException(
                        "Processed compile batch identifiers must be unique non-negative values.",
                        nameof(processedBatchIds));
                }

                batchIds[index] = batchId;
            }

            var assemblies =
                new CompileLifecycleDiagnosticsAssemblyCheckpoint[
                    processedAssemblies.Count];
            var assemblyKeys = new HashSet<(long, string)>();
            var errorCount = 0;
            var warningCount = 0;
            UnityEditorPrimaryDiagnostic primaryDiagnostic = null;
            for (var index = 0; index < processedAssemblies.Count; index++)
            {
                var assembly = processedAssemblies[index]
                    ?? throw new ArgumentException(
                        "Processed compile assemblies must not contain null.",
                        nameof(processedAssemblies));
                if (!assemblyKeys.Add((
                        assembly.BatchId,
                        assembly.AssemblyIdentity)))
                {
                    throw new ArgumentException(
                        "Processed compile assembly identities must be unique within a batch.",
                        nameof(processedAssemblies));
                }
                if (assembly.BatchId != activeBatchId
                    && !uniqueBatchIds.Contains(assembly.BatchId))
                {
                    throw new ArgumentException(
                        "Processed compile assembly must belong to the active or a completed batch.",
                        nameof(processedAssemblies));
                }

                errorCount = checked(errorCount + assembly.ErrorCount);
                warningCount = checked(
                    warningCount + assembly.WarningCount);
                primaryDiagnostic ??= assembly.PrimaryDiagnostic;
                assemblies[index] = assembly;
            }

            if (!started
                && (activeBatchId.HasValue
                    || batchIds.Length != 0
                    || assemblies.Length != 0))
            {
                throw new ArgumentException(
                    "Compile callback evidence cannot exist before compilation starts.",
                    nameof(started));
            }
            if (started
                && !activeBatchId.HasValue
                && batchIds.Length == 0)
            {
                throw new ArgumentException(
                    "Started compile diagnostics require an active or completed batch.",
                    nameof(started));
            }
            if (completed && activeBatchId.HasValue)
            {
                throw new ArgumentException(
                    "Completed compile diagnostics cannot retain an active batch.",
                    nameof(completed));
            }
            if (!completed && started && !activeBatchId.HasValue)
            {
                throw new ArgumentException(
                    "Incomplete started compile diagnostics require an active batch.",
                    nameof(activeBatchId));
            }

            Started = started;
            Completed = completed;
            ActiveBatchId = activeBatchId;
            ProcessedBatchIds = batchIds;
            ProcessedAssemblies = assemblies;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            PrimaryDiagnostic = primaryDiagnostic;
        }

        public static CompileLifecycleDiagnosticsCheckpoint Empty { get; } =
            new CompileLifecycleDiagnosticsCheckpoint(
                started: false,
                completed: false,
                activeBatchId: null,
                processedBatchIds: Array.Empty<long>(),
                processedAssemblies:
                    Array.Empty<
                        CompileLifecycleDiagnosticsAssemblyCheckpoint>());

        public static CompileLifecycleDiagnosticsCheckpoint
            NoCompilationRequired { get; } =
                new CompileLifecycleDiagnosticsCheckpoint(
                    started: false,
                    completed: true,
                    activeBatchId: null,
                    processedBatchIds: Array.Empty<long>(),
                    processedAssemblies:
                        Array.Empty<
                            CompileLifecycleDiagnosticsAssemblyCheckpoint>());

        public bool Started { get; }

        public bool Completed { get; }

        public long? ActiveBatchId { get; }

        public IReadOnlyList<long> ProcessedBatchIds { get; }

        public IReadOnlyList<CompileLifecycleDiagnosticsAssemblyCheckpoint>
            ProcessedAssemblies { get; }

        [JsonIgnore]
        public int ErrorCount { get; }

        [JsonIgnore]
        public int WarningCount { get; }

        [JsonIgnore]
        public UnityEditorPrimaryDiagnostic PrimaryDiagnostic { get; }
    }

    /// <summary>
    /// Retains the diagnostic summary of one Unity assembly callback under
    /// its stable compile batch and assembly identities.
    /// </summary>
    internal sealed record CompileLifecycleDiagnosticsAssemblyCheckpoint
    {
        [JsonConstructor]
        public CompileLifecycleDiagnosticsAssemblyCheckpoint (
            long batchId,
            string assemblyIdentity,
            int errorCount,
            int warningCount,
            UnityEditorPrimaryDiagnostic primaryDiagnostic)
        {
            if (batchId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(batchId),
                    batchId,
                    "Compile batch identifier must not be negative.");
            }
            if (string.IsNullOrWhiteSpace(assemblyIdentity))
            {
                throw new ArgumentException(
                    "Compile assembly identity must not be empty.",
                    nameof(assemblyIdentity));
            }
            if (errorCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(errorCount),
                    errorCount,
                    "Compile error count must not be negative.");
            }
            if (warningCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(warningCount),
                    warningCount,
                    "Compile warning count must not be negative.");
            }

            BatchId = batchId;
            AssemblyIdentity = assemblyIdentity;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            PrimaryDiagnostic = primaryDiagnostic;
        }

        public long BatchId { get; }

        public string AssemblyIdentity { get; }

        public int ErrorCount { get; }

        public int WarningCount { get; }

        public UnityEditorPrimaryDiagnostic PrimaryDiagnostic { get; }
    }

}
