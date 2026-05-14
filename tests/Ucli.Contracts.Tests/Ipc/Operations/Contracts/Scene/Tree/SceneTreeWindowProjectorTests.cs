using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class SceneTreeWindowProjectorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Apply_WhenWindowStartsAtDescendant_ReturnsDescendantAsFragmentRoot ()
    {
        var roots = new[]
        {
            Node(
                "Root",
                Node(
                    "Child",
                    Node("Grandchild"))),
        };

        var result = SceneTreeWindowProjector.Apply(
            roots,
            new BoundedWindowOptions(
                All: false,
                Limit: 1,
                Cursor: BoundedWindowCursorCodec.Encode(2),
                Offset: 2));

        var root = Assert.Single(result.Items);
        Assert.Equal("Grandchild", root.Name);
        Assert.Equal(IndexSceneTreeLiteNodeChildrenStateValues.Complete, root.ChildrenState);
        Assert.True(result.Window.IsComplete);
        Assert.Equal(3, result.Window.TotalCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Apply_WhenDirectChildFallsOutsideWindow_MarksParentAsTruncatedByWindow ()
    {
        var roots = new[]
        {
            Node(
                "Root",
                Node("First"),
                Node("Second")),
        };

        var result = SceneTreeWindowProjector.Apply(
            roots,
            new BoundedWindowOptions(
                All: false,
                Limit: 2,
                Cursor: null,
                Offset: 0));

        var root = Assert.Single(result.Items);
        Assert.Equal("Root", root.Name);
        Assert.Single(root.Children!);
        Assert.Equal("First", root.Children![0].Name);
        Assert.Equal(IndexSceneTreeLiteNodeChildrenStateValues.TruncatedByWindow, root.ChildrenState);
        Assert.False(result.Window.IsComplete);
        Assert.Equal(BoundedWindowCursorCodec.Encode(2), result.Window.NextCursor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Apply_WhenNodeIsNotExpandedByDepth_PreservesDepthState ()
    {
        var roots = new[]
        {
            new IndexSceneTreeLiteNodeJsonContract(
                name: "Root",
                globalObjectId: "root",
                children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                childrenState: IndexSceneTreeLiteNodeChildrenStateValues.NotExpandedByDepth),
        };

        var result = SceneTreeWindowProjector.Apply(
            roots,
            new BoundedWindowOptions(
                All: false,
                Limit: 1,
                Cursor: null,
                Offset: 0));

        var root = Assert.Single(result.Items);
        Assert.Equal(IndexSceneTreeLiteNodeChildrenStateValues.NotExpandedByDepth, root.ChildrenState);
        Assert.True(result.Window.IsComplete);
    }

    private static IndexSceneTreeLiteNodeJsonContract Node (
        string name,
        params IndexSceneTreeLiteNodeJsonContract[] children)
    {
        return new IndexSceneTreeLiteNodeJsonContract(
            name: name,
            globalObjectId: name,
            children: children,
            childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete);
    }
}
