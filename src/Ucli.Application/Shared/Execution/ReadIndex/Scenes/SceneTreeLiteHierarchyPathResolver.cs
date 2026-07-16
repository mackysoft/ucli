using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Resolves GameObject hierarchy paths from scene-tree-lite lookup nodes. </summary>
internal static class SceneTreeLiteHierarchyPathResolver
{
    /// <summary> Resolves one hierarchy path to the unique GlobalObjectId stored in scene-tree-lite data. </summary>
    /// <param name="roots"> The scene root nodes. Empty roots produce a failed result. </param>
    /// <param name="hierarchyPath"> The validated slash-separated GameObject hierarchy path. </param>
    /// <returns> A successful result when exactly one node matches the full path and has a GlobalObjectId; otherwise a failed result. </returns>
    /// <exception cref="ArgumentNullException"> <paramref name="roots" /> or <paramref name="hierarchyPath" /> is <see langword="null" />. </exception>
    public static SceneTreeLiteHierarchyPathResolutionResult Resolve (
        IReadOnlyList<SceneTreeLiteNode> roots,
        UnityHierarchyPath hierarchyPath)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(hierarchyPath);

        var hierarchyPathValue = hierarchyPath.Value;
        var segments = hierarchyPathValue.Split('/', StringSplitOptions.None);

        var candidates = new List<SceneTreeLiteNode>(roots.Count);
        AddMatches(roots, segments[0], candidates);
        if (candidates.Count == 0)
        {
            return SceneTreeLiteHierarchyPathResolutionResult.Failure($"Hierarchy path '{hierarchyPathValue}' did not match a GameObject.");
        }

        for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            if (segmentIndex == 0)
            {
                continue;
            }

            var segment = segments[segmentIndex];
            var nextCandidates = new List<SceneTreeLiteNode>();
            for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                AddMatches(candidates[candidateIndex].Children, segment, nextCandidates);
            }

            if (nextCandidates.Count == 0)
            {
                return SceneTreeLiteHierarchyPathResolutionResult.Failure($"Hierarchy path '{hierarchyPathValue}' did not match a GameObject.");
            }

            candidates = nextCandidates;
        }

        if (candidates.Count > 1)
        {
            return SceneTreeLiteHierarchyPathResolutionResult.Failure($"Hierarchy path '{hierarchyPathValue}' matched multiple GameObjects.");
        }

        var current = candidates[0];
        if (current.GlobalObjectId is not { } globalObjectId)
        {
            return SceneTreeLiteHierarchyPathResolutionResult.Failure($"Hierarchy path '{hierarchyPathValue}' does not have a GlobalObjectId in scene-tree-lite.");
        }

        return SceneTreeLiteHierarchyPathResolutionResult.Success(globalObjectId);
    }

    private static void AddMatches (
        IReadOnlyList<SceneTreeLiteNode> nodes,
        string segment,
        List<SceneTreeLiteNode> matches)
    {
        for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            var node = nodes[nodeIndex];
            if (node.Name == segment)
            {
                matches.Add(node);
            }
        }
    }
}
