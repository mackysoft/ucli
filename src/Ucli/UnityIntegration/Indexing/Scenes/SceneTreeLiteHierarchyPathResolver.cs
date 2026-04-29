using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Resolves GameObject hierarchy paths from scene-tree-lite lookup nodes. </summary>
internal static class SceneTreeLiteHierarchyPathResolver
{
    /// <summary> Resolves one hierarchy path to the unique GlobalObjectId stored in scene-tree-lite data. </summary>
    /// <param name="roots"> The scene root nodes. Empty roots produce a failed result. </param>
    /// <param name="hierarchyPath"> The non-empty slash-separated GameObject hierarchy path. </param>
    /// <returns> A successful result when exactly one node matches the full path and has a GlobalObjectId; otherwise a failed result. </returns>
    /// <exception cref="ArgumentNullException"> <paramref name="roots" /> or <paramref name="hierarchyPath" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> <paramref name="hierarchyPath" /> is empty or whitespace. </exception>
    public static SceneTreeLiteHierarchyPathResolutionResult Resolve (
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        string hierarchyPath)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentException.ThrowIfNullOrWhiteSpace(hierarchyPath);

        var segments = hierarchyPath.Split('/', StringSplitOptions.None);
        for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            if (string.IsNullOrEmpty(segments[segmentIndex]))
            {
                return SceneTreeLiteHierarchyPathResolutionResult.Failure("Hierarchy path must not contain empty segments.");
            }
        }

        var candidates = new List<IndexSceneTreeLiteNodeJsonContract>(roots.Count);
        AddMatches(roots, segments[0], candidates);
        if (candidates.Count == 0)
        {
            return SceneTreeLiteHierarchyPathResolutionResult.Failure($"Hierarchy path '{hierarchyPath}' did not match a GameObject.");
        }

        for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            if (segmentIndex == 0)
            {
                continue;
            }

            var segment = segments[segmentIndex];
            var nextCandidates = new List<IndexSceneTreeLiteNodeJsonContract>();
            for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                AddMatches(candidates[candidateIndex].Children ?? [], segment, nextCandidates);
            }

            if (nextCandidates.Count == 0)
            {
                return SceneTreeLiteHierarchyPathResolutionResult.Failure($"Hierarchy path '{hierarchyPath}' did not match a GameObject.");
            }

            candidates = nextCandidates;
        }

        if (candidates.Count > 1)
        {
            return SceneTreeLiteHierarchyPathResolutionResult.Failure($"Hierarchy path '{hierarchyPath}' matched multiple GameObjects.");
        }

        var current = candidates[0];
        if (string.IsNullOrWhiteSpace(current.GlobalObjectId))
        {
            return SceneTreeLiteHierarchyPathResolutionResult.Failure($"Hierarchy path '{hierarchyPath}' does not have a GlobalObjectId in scene-tree-lite.");
        }

        return SceneTreeLiteHierarchyPathResolutionResult.Success(current.GlobalObjectId);
    }

    private static void AddMatches (
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> nodes,
        string segment,
        List<IndexSceneTreeLiteNodeJsonContract> matches)
    {
        for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            var node = nodes[nodeIndex];
            if (string.Equals(node.Name, segment, StringComparison.Ordinal))
            {
                matches.Add(node);
            }
        }
    }
}
