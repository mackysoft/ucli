using System.Reflection;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Values;

public sealed class UnityPathValueTests
{
    public static TheoryData<Type> AssetPathValueTypes => new()
    {
        typeof(UnityAssetPath),
        typeof(SceneAssetPath),
        typeof(UnityScenePath),
        typeof(PrefabAssetPath),
        typeof(ProjectSettingsAssetPath),
        typeof(UnityAssetPathPrefix),
    };

    public static TheoryData<Type, string, string> NormalizedPathCases => new()
    {
        { typeof(UnityAssetPath), @"Assets\Data\Settings.asset", "Assets/Data/Settings.asset" },
        { typeof(SceneAssetPath), @"Assets\Scenes\Main.unity", "Assets/Scenes/Main.unity" },
        { typeof(UnityScenePath), @"Assets\Scenes\Main.unity", "Assets/Scenes/Main.unity" },
        { typeof(UnityScenePath), @"Packages\com.example\Scenes\Main.unity", "Packages/com.example/Scenes/Main.unity" },
        { typeof(PrefabAssetPath), @"Assets\Prefabs\Player.prefab", "Assets/Prefabs/Player.prefab" },
        { typeof(ProjectSettingsAssetPath), @"ProjectSettings\TagManager.asset", "ProjectSettings/TagManager.asset" },
        { typeof(UnityAssetPathPrefix), "Assets", "Assets" },
        { typeof(UnityAssetPathPrefix), @"Assets\Data", "Assets/Data" },
    };

    public static TheoryData<Type, string> InvalidPathCases => new()
    {
        { typeof(UnityAssetPath), "Assets" },
        { typeof(UnityAssetPath), "Packages/Data/Settings.asset" },
        { typeof(UnityAssetPath), "Assets/../Settings.asset" },
        { typeof(SceneAssetPath), "Assets/Scenes/Main.prefab" },
        { typeof(SceneAssetPath), "Assets/Scenes/Main.UNITY" },
        { typeof(UnityScenePath), "Packages/com.example/Scenes/Main.prefab" },
        { typeof(UnityScenePath), "ProjectSettings/Scenes/Main.unity" },
        { typeof(PrefabAssetPath), "Assets/Prefabs/Player.unity" },
        { typeof(ProjectSettingsAssetPath), "ProjectSettings" },
        { typeof(ProjectSettingsAssetPath), "Assets/TagManager.asset" },
        { typeof(ProjectSettingsAssetPath), "ProjectSettings/../TagManager.asset" },
        { typeof(UnityAssetPathPrefix), "Packages/com.example" },
        { typeof(UnityAssetPathPrefix), "Assets//Data" },
        { typeof(UnityHierarchyPath), "/Root" },
        { typeof(UnityHierarchyPath), "Root/" },
        { typeof(UnityHierarchyPath), "Root//Child" },
    };

    [Theory]
    [MemberData(nameof(NormalizedPathCases))]
    [Trait("Size", "Small")]
    public void Constructor_WhenPathUsesAlternateSeparators_StoresCanonicalPath (
        Type valueType,
        string input,
        string expected)
    {
        var value = Assert.IsAssignableFrom<UcliStringValue>(Activator.CreateInstance(valueType, input));

        Assert.Equal(expected, value.Value);
    }

    [Theory]
    [MemberData(nameof(InvalidPathCases))]
    [Trait("Size", "Small")]
    public void Constructor_WhenPathViolatesTypeInvariant_ThrowsArgumentException (
        Type valueType,
        string input)
    {
        var exception = Assert.Throws<TargetInvocationException>(
            () => Activator.CreateInstance(valueType, input));
        var argumentException = Assert.IsAssignableFrom<ArgumentException>(exception.InnerException);

        Assert.Equal("value", argumentException.ParamName);
    }

    [Theory]
    [MemberData(nameof(AssetPathValueTypes))]
    [Trait("Size", "Small")]
    public void Metadata_WhenAssetPathTypeIsInspected_DeclaresOnlyLexicalConstraints (Type valueType)
    {
        var constraints = valueType.GetCustomAttributes<UcliInputConstraintAttribute>();

        Assert.Equal(
            new[]
            {
                UcliOperationInputConstraintKind.NonEmpty,
                UcliOperationInputConstraintKind.ProjectRelativePath,
            },
            constraints.Select(static constraint => constraint.Kind));
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
