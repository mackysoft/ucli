namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Provides shared helpers used by scene-tree-lite access services. </summary>
internal static class SceneTreeLiteAccessUtilities
{
    /// <summary> Returns one tree snapshot trimmed to the requested depth. </summary>
    public static IReadOnlyList<SceneTreeLiteNode> TrimToDepth (
        IReadOnlyList<SceneTreeLiteNode> roots,
        int? depth)
    {
        ArgumentNullException.ThrowIfNull(roots);

        if (depth == null)
        {
            return roots;
        }

        var trimmedRoots = new SceneTreeLiteNode[roots.Count];
        for (var i = 0; i < roots.Count; i++)
        {
            trimmedRoots[i] = TrimNode(roots[i], currentDepth: 0, maxDepth: depth.Value);
        }

        return trimmedRoots;
    }

    private static SceneTreeLiteNode TrimNode (
        SceneTreeLiteNode node,
        int currentDepth,
        int maxDepth)
    {
        var sourceChildren = node.Children;
        var children = currentDepth >= maxDepth
            ? Array.Empty<SceneTreeLiteNode>()
            : TrimChildren(sourceChildren, currentDepth + 1, maxDepth);
        var childrenState = ResolveChildrenState(node.ChildrenState, sourceChildren.Count, currentDepth, maxDepth);
        return new SceneTreeLiteNode(
            node.Name,
            node.GlobalObjectId,
            children,
            childrenState);
    }

    private static IReadOnlyList<SceneTreeLiteNode> TrimChildren (
        IReadOnlyList<SceneTreeLiteNode> children,
        int childDepth,
        int maxDepth)
    {
        if (children.Count == 0)
        {
            return Array.Empty<SceneTreeLiteNode>();
        }

        var trimmedChildren = new SceneTreeLiteNode[children.Count];
        for (var i = 0; i < children.Count; i++)
        {
            trimmedChildren[i] = TrimNode(children[i], childDepth, maxDepth);
        }

        return trimmedChildren;
    }

    private static IndexSceneTreeLiteNodeChildrenState ResolveChildrenState (
        IndexSceneTreeLiteNodeChildrenState sourceChildrenState,
        int sourceChildCount,
        int currentDepth,
        int maxDepth)
    {
        if (currentDepth >= maxDepth && sourceChildCount > 0)
        {
            return IndexSceneTreeLiteNodeChildrenState.NotExpandedByDepth;
        }

        if (sourceChildrenState == IndexSceneTreeLiteNodeChildrenState.Unknown)
        {
            return IndexSceneTreeLiteNodeChildrenState.Unknown;
        }

        if (sourceChildrenState == IndexSceneTreeLiteNodeChildrenState.NotExpandedByDepth)
        {
            return IndexSceneTreeLiteNodeChildrenState.NotExpandedByDepth;
        }

        return IndexSceneTreeLiteNodeChildrenState.Complete;
    }

}
