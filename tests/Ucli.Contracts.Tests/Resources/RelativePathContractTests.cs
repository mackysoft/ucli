namespace MackySoft.Ucli.Contracts.Tests.Resources;

public sealed class RelativePathContractTests
{
    private static readonly string[] InvalidRelativePaths =
    [
        " Assets/Scenes/Main.unity",
        "Assets/Scenes/Main.unity ",
        "Assets//Scenes/Main.unity",
        "Assets/./Scenes/Main.unity",
        "Assets/../Scenes/Main.unity",
        "/Assets/Scenes/Main.unity",
        "C:/Project/Assets/Scenes/Main.unity",
        "Assets/Scenes/Main:Dev.unity",
    ];

    private static readonly string[] NormalizedRelativePaths =
    [
        "Assets/Scenes/Main.unity",
        "Packages/com.example/package.json",
        "ProjectSettings/ProjectSettings.asset",
    ];

    private static readonly string?[] NotNormalizedRelativePaths =
    [
        null,
        "",
        " ",
        @"Assets\Scenes\Main.unity",
        "Assets//Scenes/Main.unity",
        "Assets/../Scenes/Main.unity",
    ];

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

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WhenPathIsInvalid_ReturnsFalse ()
    {
        foreach (string path in InvalidRelativePaths)
        {
            var result = RelativePathContract.TryNormalize(path, out var normalizedPath);

            Assert.False(result);
            Assert.Equal(string.Empty, normalizedPath);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNormalized_WhenPathIsNormalizedRelativePath_ReturnsTrue ()
    {
        foreach (string path in NormalizedRelativePaths)
        {
            var result = RelativePathContract.IsNormalized(path);

            Assert.True(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNormalized_WhenPathIsNotNormalizedRelativePath_ReturnsFalse ()
    {
        foreach (string? path in NotNormalizedRelativePaths)
        {
            var result = RelativePathContract.IsNormalized(path);

            Assert.False(result);
        }
    }
}
