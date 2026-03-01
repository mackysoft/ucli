#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Represents one compact-token header model. </summary>
    /// <param name="Algorithm"> The signing algorithm identifier. </param>
    /// <param name="KeyId"> The key identifier. </param>
    /// <param name="Type"> The token type identifier. </param>
    internal sealed record PlanTokenHeader (
        string Algorithm,
        string KeyId,
        string Type);
}
