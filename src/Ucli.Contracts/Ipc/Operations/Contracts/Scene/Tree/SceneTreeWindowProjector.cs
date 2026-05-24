using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Applies bounded preorder traversal windows to scene-tree results. </summary>
internal static class SceneTreeWindowProjector
{
    /// <summary> Applies a bounded preorder traversal window to scene roots. </summary>
    public static BoundedWindowResult<IndexSceneTreeLiteNodeJsonContract> Apply (
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        BoundedWindowOptions options)
    {
        if (roots == null)
        {
            throw new ArgumentNullException(nameof(roots));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var flatNodes = Flatten(roots);

        if (options.All)
        {
            return CreateAllResult(roots, flatNodes.Count);
        }

        return ApplyPage(flatNodes, options);
    }

    private static IReadOnlyList<FlatNode> Flatten (IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots)
    {
        var flatNodes = new List<FlatNode>();
        for (var i = 0; i < roots.Count; i++)
        {
            AddNode(roots[i], parentIndex: -1, flatNodes);
        }

        return flatNodes;
    }

    private static BoundedWindowResult<IndexSceneTreeLiteNodeJsonContract> CreateAllResult (
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        int totalCount)
    {
        return new BoundedWindowResult<IndexSceneTreeLiteNodeJsonContract>(
            roots,
            new BoundedWindow(
                limit: null,
                cursor: null,
                nextCursor: null,
                isComplete: true,
                totalCount: totalCount));
    }

    private static BoundedWindowResult<IndexSceneTreeLiteNodeJsonContract> ApplyPage (
        IReadOnlyList<FlatNode> flatNodes,
        BoundedWindowOptions options)
    {
        var offset = Math.Min(options.Offset, flatNodes.Count);
        var remaining = flatNodes.Count - offset;
        var count = Math.Min(options.Limit, remaining);
        var endExclusive = offset + count;
        var isComplete = endExclusive >= flatNodes.Count;
        var projectedRoots = ProjectRoots(flatNodes, offset, endExclusive);

        return new BoundedWindowResult<IndexSceneTreeLiteNodeJsonContract>(
            projectedRoots,
            new BoundedWindow(
                limit: options.Limit,
                cursor: options.Cursor,
                nextCursor: isComplete ? null : BoundedWindowCursorCodec.Encode(endExclusive),
                isComplete: isComplete,
                totalCount: flatNodes.Count));
    }

    private static int AddNode (
        IndexSceneTreeLiteNodeJsonContract node,
        int parentIndex,
        List<FlatNode> flatNodes)
    {
        var index = flatNodes.Count;
        flatNodes.Add(new FlatNode(node, parentIndex));

        var children = node.Children ?? Array.Empty<IndexSceneTreeLiteNodeJsonContract>();
        for (var i = 0; i < children.Count; i++)
        {
            flatNodes[index].ChildIndices.Add(AddNode(children[i], index, flatNodes));
        }

        return index;
    }

    private static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> ProjectRoots (
        IReadOnlyList<FlatNode> flatNodes,
        int startInclusive,
        int endExclusive)
    {
        if (startInclusive >= endExclusive)
        {
            return Array.Empty<IndexSceneTreeLiteNodeJsonContract>();
        }

        var roots = new List<IndexSceneTreeLiteNodeJsonContract>();
        for (var i = startInclusive; i < endExclusive; i++)
        {
            var parentIndex = flatNodes[i].ParentIndex;
            if (parentIndex < startInclusive || parentIndex >= endExclusive)
            {
                roots.Add(ProjectNode(flatNodes, i, startInclusive, endExclusive));
            }
        }

        return roots;
    }

    private static IndexSceneTreeLiteNodeJsonContract ProjectNode (
        IReadOnlyList<FlatNode> flatNodes,
        int index,
        int startInclusive,
        int endExclusive)
    {
        var node = flatNodes[index].Node;
        var childIndices = flatNodes[index].ChildIndices;
        var projectedChildren = new List<IndexSceneTreeLiteNodeJsonContract>(childIndices.Count);
        for (var i = 0; i < childIndices.Count; i++)
        {
            var childIndex = childIndices[i];
            if (childIndex >= startInclusive && childIndex < endExclusive)
            {
                projectedChildren.Add(ProjectNode(flatNodes, childIndex, startInclusive, endExclusive));
            }
        }

        return new IndexSceneTreeLiteNodeJsonContract(
            name: node.Name,
            globalObjectId: node.GlobalObjectId,
            children: projectedChildren.Count == 0 ? Array.Empty<IndexSceneTreeLiteNodeJsonContract>() : projectedChildren.ToArray(),
            childrenState: ResolveChildrenState(node.ChildrenState, childIndices.Count, projectedChildren.Count));
    }

    private static string ResolveChildrenState (
        string? sourceChildrenState,
        int sourceChildCount,
        int projectedChildCount)
    {
        if (string.Equals(sourceChildrenState, IndexSceneTreeLiteNodeChildrenStateValues.NotExpandedByDepth, StringComparison.Ordinal))
        {
            return IndexSceneTreeLiteNodeChildrenStateValues.NotExpandedByDepth;
        }

        if (sourceChildCount > projectedChildCount)
        {
            return IndexSceneTreeLiteNodeChildrenStateValues.TruncatedByWindow;
        }

        if (string.Equals(sourceChildrenState, IndexSceneTreeLiteNodeChildrenStateValues.Unknown, StringComparison.Ordinal))
        {
            return IndexSceneTreeLiteNodeChildrenStateValues.Unknown;
        }

        return IndexSceneTreeLiteNodeChildrenStateValues.Complete;
    }

    private sealed class FlatNode
    {
        public FlatNode (
            IndexSceneTreeLiteNodeJsonContract node,
            int parentIndex)
        {
            Node = node;
            ParentIndex = parentIndex;
        }

        public IndexSceneTreeLiteNodeJsonContract Node { get; }

        public int ParentIndex { get; }

        public List<int> ChildIndices { get; } = new();
    }
}
