using ConsoleAppFramework;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Hosting.Cli.Options;

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class UnityAssetPathArgumentParserAttribute : Attribute, IArgumentParser<UnityAssetPath?>
{
    public static bool TryParse (ReadOnlySpan<char> value, out UnityAssetPath? result) =>
        UnityAssetPath.TryParse(value.ToString(), out result);
}

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class ProjectSettingsAssetPathArgumentParserAttribute : Attribute, IArgumentParser<ProjectSettingsAssetPath?>
{
    public static bool TryParse (ReadOnlySpan<char> value, out ProjectSettingsAssetPath? result) =>
        ProjectSettingsAssetPath.TryParse(value.ToString(), out result);
}

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class SceneAssetPathArgumentParserAttribute : Attribute, IArgumentParser<SceneAssetPath?>
{
    public static bool TryParse (ReadOnlySpan<char> value, out SceneAssetPath? result) =>
        SceneAssetPath.TryParse(value.ToString(), out result);
}

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class PrefabAssetPathArgumentParserAttribute : Attribute, IArgumentParser<PrefabAssetPath?>
{
    public static bool TryParse (ReadOnlySpan<char> value, out PrefabAssetPath? result) =>
        PrefabAssetPath.TryParse(value.ToString(), out result);
}

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class UnityHierarchyPathArgumentParserAttribute : Attribute, IArgumentParser<UnityHierarchyPath?>
{
    public static bool TryParse (ReadOnlySpan<char> value, out UnityHierarchyPath? result) =>
        UnityHierarchyPath.TryParse(value.ToString(), out result);
}

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class UnityScenePathArgumentParserAttribute : Attribute, IArgumentParser<UnityScenePath?>
{
    public static bool TryParse (ReadOnlySpan<char> value, out UnityScenePath? result) =>
        UnityScenePath.TryParse(value.ToString(), out result);
}

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class UnityAssetPathPrefixArgumentParserAttribute : Attribute, IArgumentParser<UnityAssetPathPrefix?>
{
    public static bool TryParse (ReadOnlySpan<char> value, out UnityAssetPathPrefix? result)
    {
        try
        {
            result = new UnityAssetPathPrefix(value.ToString());
            return true;
        }
        catch (ArgumentException)
        {
            result = null;
            return false;
        }
    }
}
