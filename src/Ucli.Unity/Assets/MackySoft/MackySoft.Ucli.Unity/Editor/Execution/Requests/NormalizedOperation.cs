using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Represents one normalized operation entry. </summary>
    /// <param name="Id"> The operation identifier. </param>
    /// <param name="Op"> The operation name. </param>
    /// <param name="Args"> The normalized operation arguments JSON object. </param>
    /// <param name="As"> The optional alias exposed to later operations. </param>
    /// <param name="Expect"> The optional shared expectation constraints. </param>
    /// <param name="InternalExecutionKey"> The optional request-internal primitive identity used by plan-time registries that must distinguish primitives sharing one public step id. </param>
    /// <param name="AllowRequestLocalAliases"> Whether operation args may reference request-local aliases created by edit lowering. </param>
    public sealed record NormalizedOperation (
        string Id,
        string Op,
        JsonElement Args,
        string? As,
        NormalizedExpectation? Expect,
        string? InternalExecutionKey = null,
        bool AllowRequestLocalAliases = true)
    {
        /// <summary>
        /// Gets the request-internal execution key used by plan-time registries.
        /// </summary>
        public string EffectiveExecutionKey => string.IsNullOrWhiteSpace(InternalExecutionKey) ? Id : InternalExecutionKey!;
    }
}
