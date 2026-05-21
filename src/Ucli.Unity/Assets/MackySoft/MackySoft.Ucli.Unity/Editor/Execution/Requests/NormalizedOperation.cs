using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Represents one normalized operation entry. </summary>
    public sealed record NormalizedOperation
    {
        /// <summary> Initializes a new instance of the <see cref="NormalizedOperation" /> class. </summary>
        /// <param name="Id"> The operation identifier. </param>
        /// <param name="Op"> The operation name. </param>
        /// <param name="Args"> The normalized operation arguments JSON object. </param>
        /// <param name="As"> The optional alias exposed to later operations. </param>
        /// <param name="Expect"> The optional shared expectation constraints. </param>
        /// <param name="InternalExecutionKey"> The optional request-internal primitive identity used by plan-time registries that must distinguish primitives sharing one public step id. </param>
        /// <param name="AllowRequestLocalAliases"> Whether operation args may reference request-local aliases created by edit lowering. </param>
        /// <param name="SourceKind"> The public source-step kind that produced this primitive operation. </param>
        /// <param name="SuppressPersistenceReporting"> Whether touched resources, read invalidations, and persisted observations should be suppressed from public response aggregation. </param>
        /// <param name="SuppressScenePersistenceReporting"> Whether scene touched resources and scene read invalidations should be suppressed while preserving other persistence reporting. </param>
        /// <param name="AllowExplicitPrefabAssetMutation"> Whether this operation may record request-attributed Prefab override changes that need an explicit target Prefab asset mutation. </param>
        public NormalizedOperation (
            string Id,
            string Op,
            JsonElement Args,
            string? As,
            NormalizedExpectation? Expect,
            string? InternalExecutionKey = null,
            bool AllowRequestLocalAliases = true,
            SourceStepKind SourceKind = SourceStepKind.Op,
            bool SuppressPersistenceReporting = false,
            bool SuppressScenePersistenceReporting = false,
            bool AllowExplicitPrefabAssetMutation = false)
        {
            this.Id = Id;
            this.Op = Op;
            this.Args = Args;
            this.As = As;
            this.Expect = Expect;
            this.InternalExecutionKey = InternalExecutionKey;
            this.AllowRequestLocalAliases = AllowRequestLocalAliases;
            this.SourceKind = SourceKind;
            this.SuppressPersistenceReporting = SuppressPersistenceReporting;
            this.SuppressScenePersistenceReporting = SuppressScenePersistenceReporting;
            this.AllowExplicitPrefabAssetMutation = AllowExplicitPrefabAssetMutation;
        }

        /// <summary> Identifies the public source-step kind that produced one primitive operation. </summary>
        public enum SourceStepKind
        {
            /// <summary> The primitive came from a raw <c>kind:"op"</c> step. </summary>
            Op = 0,

            /// <summary> The primitive came from a <c>kind:"edit"</c> lowering path. </summary>
            Edit = 1,
        }

        /// <summary> Gets the operation identifier. </summary>
        public string Id { get; init; }

        /// <summary> Gets the operation name. </summary>
        public string Op { get; init; }

        /// <summary> Gets the normalized operation arguments JSON object. </summary>
        public JsonElement Args { get; init; }

        /// <summary> Gets the optional alias exposed to later operations. </summary>
        public string? As { get; init; }

        /// <summary> Gets the optional shared expectation constraints. </summary>
        public NormalizedExpectation? Expect { get; init; }

        /// <summary> Gets the optional request-internal primitive identity used by plan-time registries that must distinguish primitives sharing one public step id. </summary>
        public string? InternalExecutionKey { get; init; }

        /// <summary> Gets a value indicating whether operation args may reference request-local aliases created by edit lowering. </summary>
        public bool AllowRequestLocalAliases { get; init; }

        /// <summary> Gets the public source-step kind that produced this primitive operation. </summary>
        public SourceStepKind SourceKind { get; init; }

        /// <summary> Gets a value indicating whether persistence reporting is suppressed for public response aggregation. </summary>
        public bool SuppressPersistenceReporting { get; init; }

        /// <summary> Gets a value indicating whether scene persistence reporting is suppressed while other persistence units remain reportable. </summary>
        public bool SuppressScenePersistenceReporting { get; init; }

        /// <summary> Gets a value indicating whether explicit Prefab asset mutation fallback may be recorded for request-attributed override changes. </summary>
        public bool AllowExplicitPrefabAssetMutation { get; init; }

        /// <summary>
        /// Gets the request-internal execution key used by plan-time registries.
        /// </summary>
        public string EffectiveExecutionKey => string.IsNullOrWhiteSpace(InternalExecutionKey) ? Id : InternalExecutionKey!;
    }
}
