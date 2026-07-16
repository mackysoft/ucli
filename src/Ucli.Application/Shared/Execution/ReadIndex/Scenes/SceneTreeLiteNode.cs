using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents one validated scene-tree-lite node inside the application boundary. </summary>
internal sealed record SceneTreeLiteNode
{
    /// <summary> Initializes one validated scene-tree-lite node. </summary>
    public SceneTreeLiteNode (
        string name,
        UnityGlobalObjectId? globalObjectId,
        IReadOnlyList<SceneTreeLiteNode> children,
        IndexSceneTreeLiteNodeChildrenState childrenState)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ArgumentNullException.ThrowIfNull(children);
        if (!ContractLiteralCodec.IsDefined(childrenState))
        {
            throw new ArgumentOutOfRangeException(nameof(childrenState));
        }

        GlobalObjectId = globalObjectId;
        Children = Array.AsReadOnly(children.ToArray());
        ChildrenState = childrenState;
    }

    /// <summary> Gets the GameObject name. </summary>
    public string Name { get; }

    /// <summary> Gets the parsed GlobalObjectId, or <see langword="null" /> when unavailable. </summary>
    public UnityGlobalObjectId? GlobalObjectId { get; }

    /// <summary> Gets the validated child nodes. </summary>
    public IReadOnlyList<SceneTreeLiteNode> Children { get; }

    /// <summary> Gets the child collection completeness state. </summary>
    public IndexSceneTreeLiteNodeChildrenState ChildrenState { get; }
}
