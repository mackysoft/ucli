using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class AssetGuidContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AssetGuidReferenceArgs_WhenAssetGuidIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new AssetGuidReferenceArgs(Guid.Empty));

        Assert.Equal("assetGuid", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssetsFindMatch_WhenAssetGuidIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new AssetsFindMatch(
            assetPath: new UnityAssetPath("Assets/Data/A.asset"),
            assetGuid: Guid.Empty,
            name: "A",
            typeId: new UnityTypeId("UnityEngine.ScriptableObject, UnityEngine.CoreModule")));

        Assert.Equal("assetGuid", exception.ParamName);
    }
}
