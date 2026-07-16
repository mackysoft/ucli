using MackySoft.Ucli.UnityIntegration.Indexing.Assets;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

namespace MackySoft.Ucli.Tests;

public sealed class UcliCodeSnapshotFetchResultContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FailureFactories_WithNullRequiredCode_ThrowArgumentNullException ()
    {
        var factories = new Action[]
        {
            static () => AssetLookupSnapshotFetchResult.Failure("failure", null!),
            static () => SceneTreeLiteSnapshotFetchResult.Failure("failure", null!),
        };

        Assert.All(factories, factory => Assert.Throws<ArgumentNullException>(factory));
    }
}
