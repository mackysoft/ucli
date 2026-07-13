using System.Reflection;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations.Contracts.Values;

public sealed class UnityPathValueTests
{
    public static TheoryData<Type, string, string> NormalizedPathCases => new()
    {
        { typeof(UnityAssetPath), @"Assets\Data\Settings.asset", "Assets/Data/Settings.asset" },
        { typeof(CreatableUnityAssetPath), @"Assets\Data\Settings.asset", "Assets/Data/Settings.asset" },
        { typeof(SceneAssetPath), @"Assets\Scenes\Main.unity", "Assets/Scenes/Main.unity" },
        { typeof(PrefabAssetPath), @"Assets\Prefabs\Player.prefab", "Assets/Prefabs/Player.prefab" },
        { typeof(CreatablePrefabAssetPath), @"Assets\Prefabs\Player.prefab", "Assets/Prefabs/Player.prefab" },
        { typeof(ProjectSettingsAssetPath), @"ProjectSettings\TagManager.asset", "ProjectSettings/TagManager.asset" },
        { typeof(ProjectRelativePathPrefix), @"Assets\Data", "Assets/Data" },
    };

    public static TheoryData<Type, string> InvalidPathCases => new()
    {
        { typeof(UnityAssetPath), "Assets" },
        { typeof(UnityAssetPath), "Packages/Data/Settings.asset" },
        { typeof(UnityAssetPath), "Assets/../Settings.asset" },
        { typeof(CreatableUnityAssetPath), "Assets" },
        { typeof(CreatableUnityAssetPath), "/Assets/Data/Settings.asset" },
        { typeof(SceneAssetPath), "Assets/Scenes/Main.prefab" },
        { typeof(SceneAssetPath), "Assets/Scenes/Main.UNITY" },
        { typeof(PrefabAssetPath), "Assets/Prefabs/Player.unity" },
        { typeof(CreatablePrefabAssetPath), "Assets/Prefabs/Player.PREFAB" },
        { typeof(ProjectSettingsAssetPath), "ProjectSettings" },
        { typeof(ProjectSettingsAssetPath), "Assets/TagManager.asset" },
        { typeof(ProjectSettingsAssetPath), "ProjectSettings/../TagManager.asset" },
        { typeof(ProjectRelativePathPrefix), "Packages/com.example" },
        { typeof(ProjectRelativePathPrefix), "Assets//Data" },
        { typeof(UnityHierarchyPath), "/Root" },
        { typeof(UnityHierarchyPath), "Root/" },
        { typeof(UnityHierarchyPath), "Root//Child" },
        { typeof(UnityHierarchyPathPrefix), "/Root" },
        { typeof(UnityHierarchyPathPrefix), "Root/" },
        { typeof(UnityHierarchyPathPrefix), "Root//Child" },
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
}
