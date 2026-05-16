namespace MackySoft.Ucli.Application.Tests;

public sealed class SceneTreeLiteAccessUtilitiesTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TrimToDepth_WhenNodeStateIsUnknown_PreservesUnknownState ()
    {
        var roots = new[]
        {
            new IndexSceneTreeLiteNodeJsonContract(
                name: "Root",
                globalObjectId: "root",
                children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Unknown),
        };

        var trimmed = SceneTreeLiteAccessUtilities.TrimToDepth(roots, depth: 1);

        var root = Assert.Single(trimmed);
        Assert.Equal(IndexSceneTreeLiteNodeChildrenStateValues.Unknown, root.ChildrenState);
    }
}
