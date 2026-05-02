using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one scene-tree-lite node entry. </summary>
[UcliDescription("Scene tree node.")]
public sealed record IndexSceneTreeLiteNodeJsonContract
{
    [JsonConstructor]
    public IndexSceneTreeLiteNodeJsonContract (
        string? Name,
        string? GlobalObjectId,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? Children)
    {
        this.Name = Name;
        this.GlobalObjectId = GlobalObjectId;
        this.Children = Children;
    }

    /// <summary> Gets the GameObject name. </summary>
    [UcliRequired]
    [UcliDescription("GameObject name.")]
    [UcliMinLength(1)]
    public string? Name { get; init; }

    /// <summary> Gets the resolved GlobalObjectId, or an empty string when unavailable. </summary>
    [UcliRequired]
    [UcliDescription("Resolved Unity GlobalObjectId.")]
    [UcliMinLength(1)]
    public string? GlobalObjectId { get; init; }

    /// <summary> Gets the child nodes in hierarchy order. </summary>
    [UcliRequired]
    [UcliDescription("Child nodes in hierarchy order.")]
    public IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? Children { get; init; }
}
