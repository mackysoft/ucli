using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Values;

public sealed class UnityPathValueTests
{
    public static TheoryData<Func<string, UcliStringValue>, string, string> NormalizedPathCases => new()
    {
        { static value => new UnityAssetPath(value), @"Assets\Data\Settings.asset", "Assets/Data/Settings.asset" },
        { static value => new SceneAssetPath(value), @"Assets\Scenes\Main.unity", "Assets/Scenes/Main.unity" },
        { static value => new UnityScenePath(value), @"Assets\Scenes\Main.unity", "Assets/Scenes/Main.unity" },
        { static value => new UnityScenePath(value), @"Packages\com.example\Scenes\Main.unity", "Packages/com.example/Scenes/Main.unity" },
        { static value => new PrefabAssetPath(value), @"Assets\Prefabs\Player.prefab", "Assets/Prefabs/Player.prefab" },
        { static value => new ProjectSettingsAssetPath(value), @"ProjectSettings\TagManager.asset", "ProjectSettings/TagManager.asset" },
        { static value => new UnityAssetPathPrefix(value), "Assets", "Assets" },
        { static value => new UnityAssetPathPrefix(value), @"Assets\Data", "Assets/Data" },
    };

    public static TheoryData<Func<string, UcliStringValue>, string> InvalidPathCases => new()
    {
        { static value => new UnityAssetPath(value), "Assets" },
        { static value => new UnityAssetPath(value), "Packages/Data/Settings.asset" },
        { static value => new UnityAssetPath(value), "Assets/../Settings.asset" },
        { static value => new SceneAssetPath(value), "Assets/Scenes/Main.prefab" },
        { static value => new SceneAssetPath(value), "Assets/Scenes/Main.UNITY" },
        { static value => new UnityScenePath(value), "Packages/com.example/Scenes/Main.prefab" },
        { static value => new UnityScenePath(value), "ProjectSettings/Scenes/Main.unity" },
        { static value => new PrefabAssetPath(value), "Assets/Prefabs/Player.unity" },
        { static value => new ProjectSettingsAssetPath(value), "ProjectSettings" },
        { static value => new ProjectSettingsAssetPath(value), "Assets/TagManager.asset" },
        { static value => new ProjectSettingsAssetPath(value), "ProjectSettings/../TagManager.asset" },
        { static value => new UnityAssetPathPrefix(value), "Packages/com.example" },
        { static value => new UnityAssetPathPrefix(value), "Assets//Data" },
        { static value => new UnityHierarchyPath(value), "/Root" },
        { static value => new UnityHierarchyPath(value), "Root/" },
        { static value => new UnityHierarchyPath(value), "Root//Child" },
    };

    [Theory]
    [MemberData(nameof(NormalizedPathCases))]
    [Trait("Size", "Small")]
    public void Constructor_WhenPathUsesAlternateSeparators_StoresCanonicalPath (
        Func<string, UcliStringValue> create,
        string input,
        string expected)
    {
        var value = create(input);

        Assert.Equal(expected, value.Value);
    }

    [Theory]
    [MemberData(nameof(InvalidPathCases))]
    [Trait("Size", "Small")]
    public void Constructor_WhenPathViolatesTypeInvariant_ThrowsArgumentException (
        Func<string, UcliStringValue> create,
        string input)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => create(input));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenPathsAreValid_ReturnsCanonicalTypedValues ()
    {
        Assert.True(UnityAssetPath.TryParse(@"Assets\Data\Settings.asset", out var assetPath));
        Assert.Equal("Assets/Data/Settings.asset", assetPath.Value);
        Assert.True(SceneAssetPath.TryParse(@"Assets\Scenes\Main.unity", out var scenePath));
        Assert.Equal("Assets/Scenes/Main.unity", scenePath.Value);
        Assert.True(UnityScenePath.TryParse(@"Packages\com.example\Scenes\Main.unity", out var unityScenePath));
        Assert.Equal("Packages/com.example/Scenes/Main.unity", unityScenePath.Value);
        Assert.True(PrefabAssetPath.TryParse(@"Assets\Prefabs\Player.prefab", out var prefabPath));
        Assert.Equal("Assets/Prefabs/Player.prefab", prefabPath.Value);
        Assert.True(ProjectSettingsAssetPath.TryParse(@"ProjectSettings\TagManager.asset", out var projectSettingsPath));
        Assert.Equal("ProjectSettings/TagManager.asset", projectSettingsPath.Value);
        Assert.True(UnityHierarchyPath.TryParse("Root/Child", out var hierarchyPath));
        Assert.Equal("Root/Child", hierarchyPath.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenPathsAreInvalid_ReturnsFalseWithoutValues ()
    {
        Assert.False(UnityAssetPath.TryParse("Assets", out var assetPath));
        Assert.Null(assetPath);
        Assert.False(SceneAssetPath.TryParse("Assets/Scenes/Main.prefab", out var scenePath));
        Assert.Null(scenePath);
        Assert.False(UnityScenePath.TryParse("ProjectSettings/Scenes/Main.unity", out var unityScenePath));
        Assert.Null(unityScenePath);
        Assert.False(PrefabAssetPath.TryParse("Assets/Prefabs/Player.unity", out var prefabPath));
        Assert.Null(prefabPath);
        Assert.False(ProjectSettingsAssetPath.TryParse("Assets/TagManager.asset", out var projectSettingsPath));
        Assert.Null(projectSettingsPath);
        Assert.False(UnityHierarchyPath.TryParse("Root//Child", out var hierarchyPath));
        Assert.Null(hierarchyPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenPathsContainMalformedUtf16_ReturnsFalseWithoutThrowing ()
    {
        const string malformedCharacter = "\ud800";

        Assert.False(UnityAssetPath.TryParse($"Assets/{malformedCharacter}.asset", out var assetPath));
        Assert.Null(assetPath);
        Assert.False(SceneAssetPath.TryParse($"Assets/{malformedCharacter}.unity", out var scenePath));
        Assert.Null(scenePath);
        Assert.False(UnityScenePath.TryParse($"Packages/com.example/{malformedCharacter}.unity", out var unityScenePath));
        Assert.Null(unityScenePath);
        Assert.False(PrefabAssetPath.TryParse($"Assets/{malformedCharacter}.prefab", out var prefabPath));
        Assert.Null(prefabPath);
        Assert.False(ProjectSettingsAssetPath.TryParse(
            $"ProjectSettings/{malformedCharacter}.asset",
            out var projectSettingsPath));
        Assert.Null(projectSettingsPath);
        Assert.False(UnityHierarchyPath.TryParse($"Root/{malformedCharacter}", out var hierarchyPath));
        Assert.Null(hierarchyPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityAssetPath_CompareTo_UsesCanonicalOrdinalOrder ()
    {
        var first = new UnityAssetPath("Assets/A.asset");
        var second = new UnityAssetPath("Assets/a.asset");

        Assert.True(first.CompareTo(second) < 0);
        Assert.True(second.CompareTo(first) > 0);
        Assert.Equal(0, first.CompareTo(new UnityAssetPath(first.Value)));
    }
}
