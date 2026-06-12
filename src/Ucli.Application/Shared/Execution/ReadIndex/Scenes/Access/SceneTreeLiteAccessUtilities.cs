namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Provides shared helpers used by scene-tree-lite access services. </summary>
internal static class SceneTreeLiteAccessUtilities
{
    /// <summary> Normalizes and validates one scene path input. </summary>
    public static bool TryNormalizeScenePath (
        string scenePath,
        out string normalizedScenePath,
        out string errorMessage)
    {
        normalizedScenePath = string.Empty;
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            errorMessage = "Property 'path' must not be empty or whitespace.";
            return false;
        }

        if (!UnityAssetPathContract.TryNormalizeProjectRelativePath(scenePath, out normalizedScenePath))
        {
            errorMessage = "Property 'path' must be a project-relative path without leading or trailing whitespace, empty segments, '.' segments, or '..' segments.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary> Returns whether the path can be persisted into scene-tree-lite read-index storage. </summary>
    public static bool IsLookupEligibleScenePath (string scenePath)
    {
        return UnityAssetPathContract.IsNormalizedSceneAssetPath(scenePath);
    }

    /// <summary> Returns one tree snapshot trimmed to the requested depth. </summary>
    public static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> TrimToDepth (
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        int? depth)
    {
        ArgumentNullException.ThrowIfNull(roots);

        if (depth == null)
        {
            return roots;
        }

        var trimmedRoots = new IndexSceneTreeLiteNodeJsonContract[roots.Count];
        for (var i = 0; i < roots.Count; i++)
        {
            trimmedRoots[i] = TrimNode(roots[i], currentDepth: 0, maxDepth: depth.Value);
        }

        return trimmedRoots;
    }

    private static IndexSceneTreeLiteNodeJsonContract TrimNode (
        IndexSceneTreeLiteNodeJsonContract node,
        int currentDepth,
        int maxDepth)
    {
        var sourceChildren = node.Children ?? Array.Empty<IndexSceneTreeLiteNodeJsonContract>();
        var children = currentDepth >= maxDepth
            ? Array.Empty<IndexSceneTreeLiteNodeJsonContract>()
            : TrimChildren(sourceChildren, currentDepth + 1, maxDepth);
        var childrenState = ResolveChildrenState(node.ChildrenState, sourceChildren.Count, currentDepth, maxDepth);
        return new IndexSceneTreeLiteNodeJsonContract(
            name: node.Name,
            globalObjectId: node.GlobalObjectId,
            children: children,
            childrenState: childrenState);
    }

    private static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> TrimChildren (
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> children,
        int childDepth,
        int maxDepth)
    {
        if (children.Count == 0)
        {
            return Array.Empty<IndexSceneTreeLiteNodeJsonContract>();
        }

        var trimmedChildren = new IndexSceneTreeLiteNodeJsonContract[children.Count];
        for (var i = 0; i < children.Count; i++)
        {
            trimmedChildren[i] = TrimNode(children[i], childDepth, maxDepth);
        }

        return trimmedChildren;
    }

    private static string ResolveChildrenState (
        string? sourceChildrenState,
        int sourceChildCount,
        int currentDepth,
        int maxDepth)
    {
        if (currentDepth >= maxDepth && sourceChildCount > 0)
        {
            return IndexSceneTreeLiteNodeChildrenStateValues.NotExpandedByDepth;
        }

        if (string.Equals(sourceChildrenState, IndexSceneTreeLiteNodeChildrenStateValues.Unknown, StringComparison.Ordinal))
        {
            return IndexSceneTreeLiteNodeChildrenStateValues.Unknown;
        }

        if (string.Equals(sourceChildrenState, IndexSceneTreeLiteNodeChildrenStateValues.NotExpandedByDepth, StringComparison.Ordinal))
        {
            return IndexSceneTreeLiteNodeChildrenStateValues.NotExpandedByDepth;
        }

        return IndexSceneTreeLiteNodeChildrenStateValues.Complete;
    }

}
