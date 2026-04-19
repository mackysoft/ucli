namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one scene-tree-lite node entry. </summary>
/// <param name="Name"> The GameObject name. </param>
/// <param name="GlobalObjectId"> The resolved GlobalObjectId, or an empty string when unavailable. </param>
/// <param name="Children"> The child nodes in hierarchy order. </param>
public sealed record IndexSceneTreeLiteNodeJsonContract (
    string? Name,
    string? GlobalObjectId,
    IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? Children);