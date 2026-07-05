namespace MackySoft.Ucli.Contracts.Tests.Resources;

public sealed class UnityAssetPathContractTests
{
    private static readonly string[] NormalizedSceneAssetPaths =
    [
        "Assets/Scenes/Main.unity",
        "Assets/Main.unity",
    ];

    private static readonly string?[] InvalidNormalizedSceneAssetPaths =
    [
        null,
        "",
        " ",
        " Assets/Scenes/Main.unity",
        "Assets/Scenes/Main.unity ",
        "Assets\\Scenes\\Main.unity",
        "Assets//Scenes/Main.unity",
        "Assets/./Scenes/Main.unity",
        "Assets/../Scenes/Main.unity",
        "/Assets/Scenes/Main.unity",
        "C:/Project/Assets/Scenes/Main.unity",
        "Assets/Scenes/Main:Dev.unity",
        "Packages/Scenes/Main.unity",
        "Assets/Scenes/Main.scene",
    ];

    private static readonly NormalizePathCase[] AssetsRootOrDescendantPathCases =
    [
        new("Assets", "Assets"),
        new(@"Assets\Textures\Main.asset", "Assets/Textures/Main.asset"),
    ];

    private static readonly string[] InvalidAssetsRootOrDescendantPaths =
    [
        "Packages/com.example/package.json",
        " Assets/Main.asset",
        "Assets//Main.asset",
        "Assets/Data/Foo:Bar.asset",
    ];

    private static readonly NormalizePathCase[] AssetsDescendantPathCases =
    [
        new(@"Assets\Textures\Main.asset", "Assets/Textures/Main.asset"),
        new(@"Assets\Scenes\Main.unity", "Assets/Scenes/Main.unity"),
    ];

    private static readonly string[] InvalidAssetsDescendantPaths =
    [
        "Assets",
        "Packages/com.example/package.json",
        "Assets/../Main.asset",
        "Assets/Data/Foo:Bar.asset",
    ];

    private static readonly string[] NormalizedBuildProfileAssetPaths =
    [
        "Assets/BuildProfiles/Linux.asset",
        "Assets/Linux.asset",
    ];

    private static readonly string?[] InvalidNormalizedBuildProfileAssetPaths =
    [
        null,
        "",
        " ",
        "Assets",
        " Assets/BuildProfiles/Linux.asset",
        "Assets/BuildProfiles/Linux.asset ",
        "Assets/BuildProfiles/Linux\t.asset",
        "Assets\\BuildProfiles\\Linux.asset",
        "Assets//BuildProfiles/Linux.asset",
        "Assets/BuildProfiles/Linux.asset.meta",
        "Assets/BuildProfiles/Linux.asset.META",
        "Packages/BuildProfiles/Linux.asset",
        "/Assets/BuildProfiles/Linux.asset",
    ];

    private static readonly NormalizePathCase[] BuildProfileAssetPathCases =
    [
        new(@"Assets\BuildProfiles\Linux.asset", "Assets/BuildProfiles/Linux.asset"),
        new("Assets/BuildProfiles/Linux.asset", "Assets/BuildProfiles/Linux.asset"),
    ];

    private static readonly string[] InvalidBuildProfileAssetPaths =
    [
        "Assets",
        "Assets/BuildProfiles/Linux.asset.meta",
        "Assets/BuildProfiles/Linux.asset.META",
        "Assets/BuildProfiles/Linux\t.asset",
        "Assets/../BuildProfiles/Linux.asset",
        "Assets/BuildProfiles/Linux:Dev.asset",
    ];

    private static readonly NormalizePathCase[] SceneAssetPathCases =
    [
        new(@"Assets\Scenes\Main.unity", "Assets/Scenes/Main.unity"),
        new("Assets/Scenes/Main.unity", "Assets/Scenes/Main.unity"),
    ];

    private static readonly string[] InvalidSceneAssetPaths =
    [
        "Assets/Scenes/Main.prefab",
        "Assets/Scenes/Main.UNITY",
        "Assets",
    ];

    private static readonly string[] NormalizedPrefabAssetPaths =
    [
        "Assets/Prefabs/Main.prefab",
        "Assets/Main.prefab",
    ];

    private static readonly string[] InvalidNormalizedPrefabAssetPaths =
    [
        "Assets/Prefabs/Main.unity",
        "Assets\\Prefabs\\Main.prefab",
        "Packages/Prefabs/Main.prefab",
    ];

    private static readonly NormalizePathCase[] PrefabAssetPathCases =
    [
        new(@"Assets\Prefabs\Main.prefab", "Assets/Prefabs/Main.prefab"),
        new("Assets/Prefabs/Main.prefab", "Assets/Prefabs/Main.prefab"),
    ];

    private static readonly string[] InvalidPrefabAssetPaths =
    [
        "Assets/Prefabs/Main.unity",
        "Assets/Prefabs/Main.PREFAB",
        "Assets",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void IsNormalizedSceneAssetPath_WhenPathIsValid_ReturnsTrue ()
    {
        foreach (string path in NormalizedSceneAssetPaths)
        {
            var result = UnityAssetPathContract.IsNormalizedSceneAssetPath(path);

            Assert.True(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNormalizedSceneAssetPath_WhenPathIsInvalid_ReturnsFalse ()
    {
        foreach (string? path in InvalidNormalizedSceneAssetPaths)
        {
            var result = UnityAssetPathContract.IsNormalizedSceneAssetPath(path);

            Assert.False(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeAssetsRootOrDescendantPath_WhenPathIsValid_ReturnsNormalizedPath ()
    {
        foreach (var testCase in AssetsRootOrDescendantPathCases)
        {
            var result = UnityAssetPathContract.TryNormalizeAssetsRootOrDescendantPath(testCase.Path, out var normalizedPath);

            Assert.True(result);
            Assert.Equal(testCase.ExpectedNormalizedPath, normalizedPath);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeAssetsRootOrDescendantPath_WhenPathIsInvalid_ReturnsFalse ()
    {
        foreach (string path in InvalidAssetsRootOrDescendantPaths)
        {
            var result = UnityAssetPathContract.TryNormalizeAssetsRootOrDescendantPath(path, out var normalizedPath);

            Assert.False(result);
            Assert.Equal(string.Empty, normalizedPath);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeAssetsDescendantPath_WhenPathIsValid_ReturnsNormalizedPath ()
    {
        foreach (var testCase in AssetsDescendantPathCases)
        {
            var result = UnityAssetPathContract.TryNormalizeAssetsDescendantPath(testCase.Path, out var normalizedPath);

            Assert.True(result);
            Assert.Equal(testCase.ExpectedNormalizedPath, normalizedPath);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeAssetsDescendantPath_WhenPathIsInvalid_ReturnsFalse ()
    {
        foreach (string path in InvalidAssetsDescendantPaths)
        {
            var result = UnityAssetPathContract.TryNormalizeAssetsDescendantPath(path, out var normalizedPath);

            Assert.False(result);
            Assert.Equal(string.Empty, normalizedPath);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNormalizedBuildProfileAssetPath_WhenPathIsValid_ReturnsTrue ()
    {
        foreach (string path in NormalizedBuildProfileAssetPaths)
        {
            var result = UnityAssetPathContract.IsNormalizedBuildProfileAssetPath(path);

            Assert.True(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNormalizedBuildProfileAssetPath_WhenPathIsInvalid_ReturnsFalse ()
    {
        foreach (string? path in InvalidNormalizedBuildProfileAssetPaths)
        {
            var result = UnityAssetPathContract.IsNormalizedBuildProfileAssetPath(path);

            Assert.False(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeBuildProfileAssetPath_WhenPathIsValid_ReturnsNormalizedPath ()
    {
        foreach (var testCase in BuildProfileAssetPathCases)
        {
            var result = UnityAssetPathContract.TryNormalizeBuildProfileAssetPath(testCase.Path, out var normalizedPath);

            Assert.True(result);
            Assert.Equal(testCase.ExpectedNormalizedPath, normalizedPath);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeBuildProfileAssetPath_WhenPathIsInvalid_ReturnsFalse ()
    {
        foreach (string path in InvalidBuildProfileAssetPaths)
        {
            var result = UnityAssetPathContract.TryNormalizeBuildProfileAssetPath(path, out var normalizedPath);

            Assert.False(result);
            Assert.Equal(string.Empty, normalizedPath);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeSceneAssetPath_WhenPathIsValid_ReturnsNormalizedPath ()
    {
        foreach (var testCase in SceneAssetPathCases)
        {
            var result = UnityAssetPathContract.TryNormalizeSceneAssetPath(testCase.Path, out var normalizedPath);

            Assert.True(result);
            Assert.Equal(testCase.ExpectedNormalizedPath, normalizedPath);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizeSceneAssetPath_WhenPathIsInvalid_ReturnsFalse ()
    {
        foreach (string path in InvalidSceneAssetPaths)
        {
            var result = UnityAssetPathContract.TryNormalizeSceneAssetPath(path, out var normalizedPath);

            Assert.False(result);
            Assert.Equal(string.Empty, normalizedPath);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNormalizedPrefabAssetPath_WhenPathIsValid_ReturnsTrue ()
    {
        foreach (string path in NormalizedPrefabAssetPaths)
        {
            var result = UnityAssetPathContract.IsNormalizedPrefabAssetPath(path);

            Assert.True(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNormalizedPrefabAssetPath_WhenPathIsInvalid_ReturnsFalse ()
    {
        foreach (string path in InvalidNormalizedPrefabAssetPaths)
        {
            var result = UnityAssetPathContract.IsNormalizedPrefabAssetPath(path);

            Assert.False(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizePrefabAssetPath_WhenPathIsValid_ReturnsNormalizedPath ()
    {
        foreach (var testCase in PrefabAssetPathCases)
        {
            var result = UnityAssetPathContract.TryNormalizePrefabAssetPath(testCase.Path, out var normalizedPath);

            Assert.True(result);
            Assert.Equal(testCase.ExpectedNormalizedPath, normalizedPath);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalizePrefabAssetPath_WhenPathIsInvalid_ReturnsFalse ()
    {
        foreach (string path in InvalidPrefabAssetPaths)
        {
            var result = UnityAssetPathContract.TryNormalizePrefabAssetPath(path, out var normalizedPath);

            Assert.False(result);
            Assert.Equal(string.Empty, normalizedPath);
        }
    }

    private sealed record NormalizePathCase (
        string Path,
        string ExpectedNormalizedPath);
}
