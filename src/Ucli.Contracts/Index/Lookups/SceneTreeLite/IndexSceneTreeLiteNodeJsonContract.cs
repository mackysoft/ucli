using System.Text.Json.Serialization;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one scene-tree-lite node entry. </summary>
[Description("Scene tree node.")]
public sealed record IndexSceneTreeLiteNodeJsonContract
{
    [JsonConstructor]
    public IndexSceneTreeLiteNodeJsonContract (
        string? name,
        string? globalObjectId,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? children,
        IndexSceneTreeLiteNodeChildrenState childrenState)
    {
        if (!TextVocabulary.IsDefined(childrenState))
        {
            throw new ArgumentOutOfRangeException(nameof(childrenState), childrenState, "Unsupported scene-tree-lite node children state.");
        }

        Name = name;
        GlobalObjectId = globalObjectId;
        Children = children;
        ChildrenState = childrenState;
    }

    /// <summary> Gets the GameObject name. </summary>
    [JsonRequired]
    [Description("GameObject name.")]
    public string? Name { get; init; }

    /// <summary> Gets the resolved GlobalObjectId, or an empty string when unavailable. </summary>
    [JsonRequired]
    [Description("Resolved Unity GlobalObjectId.")]
    public string? GlobalObjectId { get; init; }

    /// <summary> Gets the child nodes in hierarchy order. </summary>
    [JsonRequired]
    [Description("Child nodes in hierarchy order.")]
    public IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? Children { get; init; }

    /// <summary> Gets the child collection completeness state. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Child node completeness state.")]
    public IndexSceneTreeLiteNodeChildrenState ChildrenState { get; private init; }
}
