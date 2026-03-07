using System.Collections.Generic;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one GameObject description snapshot. </summary>
    internal sealed record GameObjectDescription (
        string Name,
        string GlobalObjectId,
        IReadOnlyList<GameObjectComponentDescription> Components,
        IReadOnlyList<GameObjectDescription> Children);
}