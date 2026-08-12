using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

namespace MackySoft.Ucli.Tests.Helpers.Indexing.Scenes;

internal static class SceneTreeLiteSourceSnapshotAssert
{
    public static void Equal (SceneTreeLiteSourceSnapshot expected, SceneTreeLiteSourceSnapshot? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.GeneratedAtUtc, actual.GeneratedAtUtc);
        Assert.Equal(expected.ScenePath, actual.ScenePath);
        Assert.Equal(expected.SourceState, actual.SourceState);
        AssertNodesEqual(expected.Roots, actual.Roots);
    }

    private static void AssertNodesEqual (
        IReadOnlyList<SceneTreeLiteNode> expected,
        IReadOnlyList<SceneTreeLiteNode> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var expectedNode = expected[i];
            var actualNode = actual[i];
            Assert.Equal(expectedNode.Name, actualNode.Name);
            Assert.Equal(expectedNode.GlobalObjectId, actualNode.GlobalObjectId);
            Assert.Equal(expectedNode.ChildrenState, actualNode.ChildrenState);
            AssertNodesEqual(expectedNode.Children, actualNode.Children);
        }
    }
}
