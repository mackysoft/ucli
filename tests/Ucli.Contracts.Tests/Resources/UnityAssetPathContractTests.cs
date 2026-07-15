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

    public static TheoryData<string, string, bool> SameOrDescendantAssetPathCases => new()
    {
        { "Assets", "Assets/Main.asset", true },
        { "Assets/Data", "Assets/Data", true },
        { "Assets/Data", "Assets/Data/Main.asset", true },
        { "Assets/Data", "Assets/DataExtra/Main.asset", false },
        { "Assets/Data", "Assets/data/Main.asset", false },
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(SameOrDescendantAssetPathCases))]
    public void IsSameOrDescendantAssetPath_WhenPathsAreNormalized_UsesOrdinalSegmentBoundaries (
        string pathPrefix,
        string assetPath,
        bool expected)
    {
        var result = UnityAssetPathContract.IsSameOrDescendantAssetPath(pathPrefix, assetPath);

        Assert.Equal(expected, result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null, "Assets/Main.asset", "normalizedPathPrefix")]
    [InlineData("Assets", null, "normalizedAssetPath")]
    public void IsSameOrDescendantAssetPath_WhenArgumentIsNull_ThrowsArgumentNullException (
        string? pathPrefix,
        string? assetPath,
        string expectedParameterName)
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => UnityAssetPathContract.IsSameOrDescendantAssetPath(pathPrefix!, assetPath!));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

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
