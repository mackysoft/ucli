using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class AssetsFindMatchTests
{
    private static readonly UnityAssetPath AssetPath = new("Assets/Data/A.asset");

    private static readonly UnityTypeId TypeId = new("UnityEngine.ScriptableObject, UnityEngine.CoreModule");

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenAssetPathIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new AssetsFindMatch(
            assetPath: null!,
            assetGuid: null,
            name: "A",
            typeId: TypeId));

        Assert.Equal("assetPath", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Size", "Small")]
    public void Constructor_WhenNameIsMissing_ThrowsArgumentException (string? name)
    {
        var exception = Assert.Throws<ArgumentException>(() => new AssetsFindMatch(
            assetPath: AssetPath,
            assetGuid: null,
            name: name!,
            typeId: TypeId));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenTypeIdIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new AssetsFindMatch(
            assetPath: AssetPath,
            assetGuid: null,
            name: "A",
            typeId: null!));

        Assert.Equal("typeId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ValueEquality_WhenEveryFieldMatches_ReturnsTrue ()
    {
        var expected = new AssetsFindMatch(AssetPath, assetGuid: null, "A", TypeId);
        var actual = new AssetsFindMatch(AssetPath, assetGuid: null, "A", TypeId);

        Assert.NotSame(expected, actual);
        Assert.Equal(expected, actual);
        Assert.Equal(expected.GetHashCode(), actual.GetHashCode());
    }
}
