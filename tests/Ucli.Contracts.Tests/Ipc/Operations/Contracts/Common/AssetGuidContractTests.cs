using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class AssetGuidContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AssetReferenceArgs_WhenAssetGuidIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new AssetReferenceArgs(
            alias: null,
            globalObjectId: null,
            assetGuid: Guid.Empty,
            assetPath: null,
            projectAssetPath: null));

        Assert.Equal("assetGuid", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSelectorArgs_WhenAssetGuidIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ResolveSelectorArgs(
            globalObjectId: null,
            assetGuid: Guid.Empty,
            assetPath: null,
            projectAssetPath: null,
            scene: null,
            prefab: null,
            hierarchyPath: null,
            componentType: null));

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
