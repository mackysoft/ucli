using System;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Represents the Play Mode exit facts persisted before its side effect and before terminal publication.
    /// </summary>
    internal sealed record PlayExitLifecycleExecutionCheckpoint
    {
        public const int CurrentSchemaVersion = 1;

        [JsonConstructor]
        public PlayExitLifecycleExecutionCheckpoint (
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
                    "Unsupported Play Mode exit checkpoint schema version.");
            }
            if (executionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Play Mode exit execution identifier must not be empty.",
                    nameof(executionId));
            }

            SchemaVersion = schemaVersion;
            ExecutionId = executionId;
            Before = before ?? throw new ArgumentNullException(nameof(before));
            SideEffectAdmitted = sideEffectAdmitted;
        }

        public int SchemaVersion { get; init; }

        public Guid ExecutionId { get; init; }

        public UnityEditorObservation Before { get; init; }

        public bool SideEffectAdmitted { get; init; }
    }
}
