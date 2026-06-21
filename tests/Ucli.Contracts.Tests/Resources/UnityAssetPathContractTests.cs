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
    [InlineData("Assets/Scenes/Main:Dev.unity")]
    [InlineData("Packages/Scenes/Main.unity")]
    [InlineData("Assets/Scenes/Main.scene")]
    public void IsNormalizedSceneAssetPath_WhenPathIsInvalid_ReturnsFalse (string? path)
    {
        var result = UnityAssetPathContract.IsNormalizedSceneAssetPath(path);

        Assert.False(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Assets", "Assets")]
    [InlineData(@"Assets\Textures\Main.asset", "Assets/Textures/Main.asset")]
    public void TryNormalizeAssetsRootOrDescendantPath_WhenPathIsValid_ReturnsNormalizedPath (
        string path,
        string expectedNormalizedPath)
    {
        var result = UnityAssetPathContract.TryNormalizeAssetsRootOrDescendantPath(path, out var normalizedPath);

        Assert.True(result);
        Assert.Equal(expectedNormalizedPath, normalizedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Packages/com.example/package.json")]
    [InlineData(" Assets/Main.asset")]
    [InlineData("Assets//Main.asset")]
    [InlineData("Assets/Data/Foo:Bar.asset")]
    public void TryNormalizeAssetsRootOrDescendantPath_WhenPathIsInvalid_ReturnsFalse (string path)
    {
        var result = UnityAssetPathContract.TryNormalizeAssetsRootOrDescendantPath(path, out var normalizedPath);

        Assert.False(result);
        Assert.Equal(string.Empty, normalizedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(@"Assets\Textures\Main.asset", "Assets/Textures/Main.asset")]
    [InlineData(@"Assets\Scenes\Main.unity", "Assets/Scenes/Main.unity")]
    public void TryNormalizeAssetsDescendantPath_WhenPathIsValid_ReturnsNormalizedPath (
        string path,
        string expectedNormalizedPath)
    {
        var result = UnityAssetPathContract.TryNormalizeAssetsDescendantPath(path, out var normalizedPath);

        Assert.True(result);
        Assert.Equal(expectedNormalizedPath, normalizedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Assets")]
    [InlineData("Packages/com.example/package.json")]
    [InlineData("Assets/../Main.asset")]
    [InlineData("Assets/Data/Foo:Bar.asset")]
    public void TryNormalizeAssetsDescendantPath_WhenPathIsInvalid_ReturnsFalse (string path)
    {
        var result = UnityAssetPathContract.TryNormalizeAssetsDescendantPath(path, out var normalizedPath);

        Assert.False(result);
        Assert.Equal(string.Empty, normalizedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Assets/BuildProfiles/Linux.asset")]
    [InlineData("Assets/Linux.asset")]
    public void IsNormalizedBuildProfileAssetPath_WhenPathIsValid_ReturnsTrue (string path)
    {
        var result = UnityAssetPathContract.IsNormalizedBuildProfileAssetPath(path);

        Assert.True(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Assets")]
    [InlineData(" Assets/BuildProfiles/Linux.asset")]
    [InlineData("Assets/BuildProfiles/Linux.asset ")]
    [InlineData("Assets/BuildProfiles/Linux\t.asset")]
    [InlineData("Assets\\BuildProfiles\\Linux.asset")]
    [InlineData("Assets//BuildProfiles/Linux.asset")]
    [InlineData("Assets/BuildProfiles/Linux.asset.meta")]
    [InlineData("Assets/BuildProfiles/Linux.asset.META")]
    [InlineData("Packages/BuildProfiles/Linux.asset")]
    [InlineData("/Assets/BuildProfiles/Linux.asset")]
    public void IsNormalizedBuildProfileAssetPath_WhenPathIsInvalid_ReturnsFalse (string? path)
    {
        var result = UnityAssetPathContract.IsNormalizedBuildProfileAssetPath(path);

        Assert.False(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(@"Assets\BuildProfiles\Linux.asset", "Assets/BuildProfiles/Linux.asset")]
    [InlineData("Assets/BuildProfiles/Linux.asset", "Assets/BuildProfiles/Linux.asset")]
    public void TryNormalizeBuildProfileAssetPath_WhenPathIsValid_ReturnsNormalizedPath (
        string path,
        string expectedNormalizedPath)
    {
        var result = UnityAssetPathContract.TryNormalizeBuildProfileAssetPath(path, out var normalizedPath);

        Assert.True(result);
        Assert.Equal(expectedNormalizedPath, normalizedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Assets")]
    [InlineData("Assets/BuildProfiles/Linux.asset.meta")]
    [InlineData("Assets/BuildProfiles/Linux.asset.META")]
    [InlineData("Assets/BuildProfiles/Linux\t.asset")]
    [InlineData("Assets/../BuildProfiles/Linux.asset")]
    [InlineData("Assets/BuildProfiles/Linux:Dev.asset")]
    public void TryNormalizeBuildProfileAssetPath_WhenPathIsInvalid_ReturnsFalse (string path)
    {
        var result = UnityAssetPathContract.TryNormalizeBuildProfileAssetPath(path, out var normalizedPath);

        Assert.False(result);
        Assert.Equal(string.Empty, normalizedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(@"Assets\Scenes\Main.unity", "Assets/Scenes/Main.unity")]
    [InlineData("Assets/Scenes/Main.unity", "Assets/Scenes/Main.unity")]
    public void TryNormalizeSceneAssetPath_WhenPathIsValid_ReturnsNormalizedPath (
        string path,
        string expectedNormalizedPath)
    {
        var result = UnityAssetPathContract.TryNormalizeSceneAssetPath(path, out var normalizedPath);

        Assert.True(result);
        Assert.Equal(expectedNormalizedPath, normalizedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Assets/Scenes/Main.prefab")]
    [InlineData("Assets/Scenes/Main.UNITY")]
    [InlineData("Assets")]
    public void TryNormalizeSceneAssetPath_WhenPathIsInvalid_ReturnsFalse (string path)
    {
        var result = UnityAssetPathContract.TryNormalizeSceneAssetPath(path, out var normalizedPath);

        Assert.False(result);
        Assert.Equal(string.Empty, normalizedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Assets/Prefabs/Main.prefab")]
    [InlineData("Assets/Main.prefab")]
    public void IsNormalizedPrefabAssetPath_WhenPathIsValid_ReturnsTrue (string path)
    {
        var result = UnityAssetPathContract.IsNormalizedPrefabAssetPath(path);

        Assert.True(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Assets/Prefabs/Main.unity")]
    [InlineData("Assets\\Prefabs\\Main.prefab")]
    [InlineData("Packages/Prefabs/Main.prefab")]
    public void IsNormalizedPrefabAssetPath_WhenPathIsInvalid_ReturnsFalse (string path)
    {
        var result = UnityAssetPathContract.IsNormalizedPrefabAssetPath(path);

        Assert.False(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(@"Assets\Prefabs\Main.prefab", "Assets/Prefabs/Main.prefab")]
    [InlineData("Assets/Prefabs/Main.prefab", "Assets/Prefabs/Main.prefab")]
    public void TryNormalizePrefabAssetPath_WhenPathIsValid_ReturnsNormalizedPath (
        string path,
        string expectedNormalizedPath)
    {
        var result = UnityAssetPathContract.TryNormalizePrefabAssetPath(path, out var normalizedPath);

        Assert.True(result);
        Assert.Equal(expectedNormalizedPath, normalizedPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Assets/Prefabs/Main.unity")]
    [InlineData("Assets/Prefabs/Main.PREFAB")]
    [InlineData("Assets")]
    public void TryNormalizePrefabAssetPath_WhenPathIsInvalid_ReturnsFalse (string path)
    {
        var result = UnityAssetPathContract.TryNormalizePrefabAssetPath(path, out var normalizedPath);

        Assert.False(result);
        Assert.Equal(string.Empty, normalizedPath);
    }
}
