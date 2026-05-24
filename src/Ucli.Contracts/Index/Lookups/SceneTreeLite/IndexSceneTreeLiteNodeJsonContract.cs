using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one scene-tree-lite node entry. </summary>
[UcliDescription("Scene tree node.")]
public sealed record IndexSceneTreeLiteNodeJsonContract
{
    [JsonConstructor]
    public IndexSceneTreeLiteNodeJsonContract (
        string? name,
        string? globalObjectId,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? children,
        string? childrenState)
    {
        Name = name;
        GlobalObjectId = globalObjectId;
        Children = children;
        ChildrenState = childrenState;
    }

    /// <summary> Gets the GameObject name. </summary>
    [UcliRequired]
    [UcliDescription("GameObject name.")]
    public string? Name { get; init; }

    /// <summary> Gets the resolved GlobalObjectId, or an empty string when unavailable. </summary>
    [UcliRequired]
    [UcliDescription("Resolved Unity GlobalObjectId.")]
    public string? GlobalObjectId { get; init; }

    /// <summary> Gets the child nodes in hierarchy order. </summary>
    [UcliRequired]
    [UcliDescription("Child nodes in hierarchy order.")]
    public IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? Children { get; init; }

    /// <summary> Gets whether the child nodes are complete or truncated. </summary>
    [UcliRequired]
    [UcliDescription("Child node completeness state. Values are complete, notExpandedByDepth, truncatedByWindow, and unknown.")]
    public string? ChildrenState { get; init; }
}
