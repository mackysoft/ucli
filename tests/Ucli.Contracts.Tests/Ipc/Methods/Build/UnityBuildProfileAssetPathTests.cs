using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Build;

public sealed class UnityBuildProfileAssetPathTests
{
    public static TheoryData<string> InvalidPaths =>
        new()
        {
            "",
            " ",
            "Assets",
            " Assets/BuildProfiles/Linux.asset",
            "Assets/BuildProfiles/Linux.asset ",
            "Assets/BuildProfiles/Linux\t.asset",
            "Assets\\BuildProfiles\\Linux.asset",
            "Assets//BuildProfiles/Linux.asset",
            "Assets/../BuildProfiles/Linux.asset",
            "Assets/BuildProfiles/Linux:Dev.asset",
            "Assets/BuildProfiles/Linux.asset.meta",
            "Assets/BuildProfiles/Linux.asset.META",
            "Packages/BuildProfiles/Linux.asset",
            "/Assets/BuildProfiles/Linux.asset",
        };

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithCanonicalPath_PreservesValue ()
    {
        var path = new UnityBuildProfileAssetPath("Assets/BuildProfiles/Linux.asset");

        Assert.Equal("Assets/BuildProfiles/Linux.asset", path.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => new UnityBuildProfileAssetPath(null!));
    }

    [Theory]
    [MemberData(nameof(InvalidPaths))]
    [Trait("Size", "Small")]
    public void Constructor_WithInvalidPath_ThrowsArgumentException (string value)
    {
        Assert.Throws<ArgumentException>(() => new UnityBuildProfileAssetPath(value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WithCanonicalPath_ReturnsTypedPath ()
    {
        var result = UnityBuildProfileAssetPath.TryParse(
            "Assets/BuildProfiles/Linux.asset",
            out var path);

        Assert.True(result);
        Assert.NotNull(path);
        Assert.Equal("Assets/BuildProfiles/Linux.asset", path.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WithNull_ReturnsFalse ()
    {
        var result = UnityBuildProfileAssetPath.TryParse(null, out var path);

        Assert.False(result);
        Assert.Null(path);
    }

    [Theory]
    [MemberData(nameof(InvalidPaths))]
    [Trait("Size", "Small")]
    public void TryParse_WithInvalidPath_ReturnsFalse (string value)
    {
        var result = UnityBuildProfileAssetPath.TryParse(value, out var path);

        Assert.False(result);
        Assert.Null(path);
    }
}
