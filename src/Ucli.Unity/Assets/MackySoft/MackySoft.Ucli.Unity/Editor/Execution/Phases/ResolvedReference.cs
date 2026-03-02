#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one resolved selector target normalized as a GlobalObjectId string. </summary>
    /// <param name="GlobalObjectId"> The normalized GlobalObjectId string. </param>
    internal sealed record ResolvedReference (string GlobalObjectId);
}
