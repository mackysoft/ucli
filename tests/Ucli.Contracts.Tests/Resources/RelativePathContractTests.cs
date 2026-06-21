namespace MackySoft.Ucli.Contracts.Tests.Resources;

public sealed class RelativePathContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WhenPathUsesBackslashes_ReturnsSlashSeparatedPath ()
    {
        var result = RelativePathContract.TryNormalize(
            @"Assets\Scenes\Main.unity",
            out var normalizedPath);

        Assert.True(result);
        Assert.Equal("Assets/Scenes/Main.unity", normalizedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(" Assets/Scenes/Main.unity")]
    [InlineData("Assets/Scenes/Main.unity ")]
    [InlineData("Assets//Scenes/Main.unity")]
    [InlineData("Assets/./Scenes/Main.unity")]
    [InlineData("Assets/../Scenes/Main.unity")]
    [InlineData("/Assets/Scenes/Main.unity")]
    [InlineData("C:/Project/Assets/Scenes/Main.unity")]
    [InlineData("Assets/Scenes/Main:Dev.unity")]
    public void TryNormalize_WhenPathIsInvalid_ReturnsFalse (string path)
    {
        var result = RelativePathContract.TryNormalize(path, out var normalizedPath);

        Assert.False(result);
        Assert.Equal(string.Empty, normalizedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Assets/Scenes/Main.unity")]
    [InlineData("Packages/com.example/package.json")]
    [InlineData("ProjectSettings/ProjectSettings.asset")]
    public void IsNormalized_WhenPathIsNormalizedRelativePath_ReturnsTrue (string path)
    {
        var result = RelativePathContract.IsNormalized(path);

        Assert.True(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(@"Assets\Scenes\Main.unity")]
    [InlineData("Assets//Scenes/Main.unity")]
    [InlineData("Assets/../Scenes/Main.unity")]
    public void IsNormalized_WhenPathIsNotNormalizedRelativePath_ReturnsFalse (string? path)
    {
        var result = RelativePathContract.IsNormalized(path);

        Assert.False(result);
    }
}
