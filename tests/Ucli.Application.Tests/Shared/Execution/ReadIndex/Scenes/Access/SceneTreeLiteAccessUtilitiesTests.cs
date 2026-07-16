using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

public sealed class SceneTreeLiteAccessUtilitiesTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void NodeConstructor_WhenChildrenStateIsUndefined_ThrowsArgumentOutOfRangeException ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SceneTreeLiteNode(
            "Root",
            globalObjectId: null,
            children: [],
            childrenState: default));

        Assert.Equal("childrenState", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TrimToDepth_WhenNodeStateIsUnknown_PreservesUnknownState ()
    {
        var roots = new[]
        {
            new SceneTreeLiteNode(
                "Root",
                new UnityGlobalObjectId("GlobalObjectId_V1-2-11111111111111111111111111111111-1-0"),
                [],
                IndexSceneTreeLiteNodeChildrenState.Unknown),
        };

        var trimmed = SceneTreeLiteAccessUtilities.TrimToDepth(roots, depth: 1);

        var root = Assert.Single(trimmed);
        Assert.Equal(IndexSceneTreeLiteNodeChildrenState.Unknown, root.ChildrenState);
    }
}
