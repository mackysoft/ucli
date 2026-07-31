using System;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Retains the Play Mode entry facts that only the entry handler may interpret.
    /// </summary>
    internal sealed record PlayEnterLifecycleExecutionCheckpoint
    {
        public const int CurrentSchemaVersion = 1;

        [JsonConstructor]
        public PlayEnterLifecycleExecutionCheckpoint (
            int schemaVersion,
            Guid executionId,
            UnityEditorObservation before,
            bool sideEffectAdmitted)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    schemaVersion,
                    "Unsupported Play Mode entry checkpoint schema version.");
            }

            if (executionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Play Mode entry execution identifier must not be empty.",
                    nameof(executionId));
            }

            SchemaVersion = schemaVersion;
            ExecutionId = executionId;
            Before = before;
            SideEffectAdmitted = sideEffectAdmitted;
            if (sideEffectAdmitted && before == null)
            {
                throw new ArgumentNullException(
                    nameof(before),
                    "An admitted Play Mode entry side effect requires its durable before snapshot.");
            }
        }

        public int SchemaVersion { get; }

        public Guid ExecutionId { get; }

        public UnityEditorObservation Before { get; }

        public bool SideEffectAdmitted { get; }
    }
}
