using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one final per-operation trace entry produced by phase execution. </summary>
    /// <param name="OpId"> The operation identifier. </param>
    /// <param name="Op"> The operation name. </param>
    /// <param name="Phase"> The final phase reached by this operation. </param>
    /// <param name="Applied"> Whether operation was applied. </param>
    /// <param name="Changed"> Whether operation produced changes. </param>
    /// <param name="Touched"> The touched persistence-unit list. </param>
    /// <param name="Failure"> The operation failure details; otherwise <see langword="null" />. </param>
    internal sealed record OperationPhaseTrace (
        string OpId,
        string Op,
        OperationPhase Phase,
        bool Applied,
        bool Changed,
        IReadOnlyList<OperationTouch> Touched,
        OperationFailure? Failure)
    {
        /// <summary> Gets the optional query result payload produced by the final phase. </summary>
        public JsonElement? Result { get; init; }

        /// <summary> Gets the read-surface invalidations emitted by the final phase. </summary>
        public IReadOnlyList<OperationReadInvalidation> ReadInvalidations { get; init; } = System.Array.Empty<OperationReadInvalidation>();

        /// <summary> Gets non-fatal diagnostics emitted by this primitive trace. </summary>
        public IReadOnlyList<OperationDiagnostic> Diagnostics { get; init; } = System.Array.Empty<OperationDiagnostic>();

        /// <summary> Gets a value indicating whether this trace observed successful persistence. </summary>
        public bool Persisted { get; init; }

        /// <summary> Gets the operation metadata facts used to validate runtime results against declared assurance. </summary>
        public ContractFacts? Contracts { get; init; }

        /// <summary> Represents the assurance facts needed for runtime result consistency checks. </summary>
        /// <param name="OperationKind"> The declared operation kind. </param>
        /// <param name="MayDirty"> Whether the operation may dirty Unity objects or project state. </param>
        /// <param name="MayPersist"> Whether the operation may persist project files. </param>
        /// <param name="TouchedKinds"> The touched-resource kind literals that may be reported. </param>
        public sealed record ContractFacts (
            UcliOperationKind OperationKind,
            bool MayDirty,
            bool MayPersist,
            IReadOnlyList<string> TouchedKinds)
        {
            /// <summary> Creates a facts snapshot from one operation metadata instance. </summary>
            /// <param name="metadata"> The operation metadata. </param>
            /// <returns> The immutable facts snapshot used by response validation. </returns>
            public static ContractFacts FromMetadata (UcliOperationMetadata metadata)
            {
                if (metadata == null)
                {
                    throw new System.ArgumentNullException(nameof(metadata));
                }

                var assurance = metadata.DescribeContract.Assurance;
                return new ContractFacts(
                    OperationKind: metadata.Kind,
                    MayDirty: assurance?.MayDirty ?? false,
                    MayPersist: assurance?.MayPersist ?? false,
                    TouchedKinds: assurance?.TouchedKinds ?? System.Array.Empty<string>());
            }
        }
    }
}
