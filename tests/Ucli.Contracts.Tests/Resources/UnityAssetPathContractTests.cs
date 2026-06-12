namespace MackySoft.Ucli.Contracts.Tests.Resources;

public sealed class UnityAssetPathContractTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Assets/Scenes/Main.unity")]
    [InlineData("Assets/Main.unity")]
    public void IsNormalizedSceneAssetPath_WhenPathIsValid_ReturnsTrue (string path)
    {
        var result = UnityAssetPathContract.IsNormalizedSceneAssetPath(path);

        Assert.True(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" Assets/Scenes/Main.unity")]
    [InlineData("Assets/Scenes/Main.unity ")]
    [InlineData("Assets\\Scenes\\Main.unity")]
    [InlineData("Assets//Scenes/Main.unity")]
    [InlineData("Assets/./Scenes/Main.unity")]
    [InlineData("Assets/../Scenes/Main.unity")]
    [InlineData("/Assets/Scenes/Main.unity")]
    [InlineData("C:/Project/Assets/Scenes/Main.unity")]
    [InlineData("Packages/Scenes/Main.unity")]
    [InlineData("Assets/Scenes/Main.scene")]
    public void IsNormalizedSceneAssetPath_WhenPathIsInvalid_ReturnsFalse (string? path)
    {
        var result = UnityAssetPathContract.IsNormalizedSceneAssetPath(path);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeProjectRelativePath_WhenPathUsesBackslashes_ReturnsSlashSeparatedPath ()
    {
        var result = UnityAssetPathContract.TryNormalizeProjectRelativePath(
            @"Assets\Scenes\Main.unity",
            out var normalizedPath);

        Assert.True(result);
        Assert.Equal("Assets/Scenes/Main.unity", normalizedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(" Assets/Scenes/Main.unity")]
    [InlineData("Assets//Scenes/Main.unity")]
    [InlineData("Assets/../Scenes/Main.unity")]
    [InlineData("/Assets/Scenes/Main.unity")]
    [InlineData("C:/Project/Assets/Scenes/Main.unity")]
    public void TryNormalizeProjectRelativePath_WhenPathIsInvalid_ReturnsFalse (string path)
    {
        var result = UnityAssetPathContract.TryNormalizeProjectRelativePath(path, out var normalizedPath);

        Assert.False(result);
        Assert.Equal(string.Empty, normalizedPath);
    }
}
