using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve.Projection;

/// <summary> Creates <c>ucli.resolve</c> operation argument payloads from normalized selectors. </summary>
internal static class ResolveSelectorOperationArgsFactory
{
    /// <summary> Creates the JSON object used as one <c>ucli.resolve</c> operation args payload. </summary>
    public static JsonElement Create (ResolveSelectorInput selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return selector switch
        {
            ResolveGlobalObjectIdSelectorInput globalObjectId => CreateArgs(
                (IpcResolveSelectorPropertyNames.GlobalObjectId, globalObjectId.GlobalObjectId)),
            ResolveAssetGuidSelectorInput assetGuid => CreateArgs(
                (IpcResolveSelectorPropertyNames.AssetGuid, assetGuid.AssetGuid)),
            ResolveAssetPathSelectorInput assetPath => CreateArgs(
                (IpcResolveSelectorPropertyNames.AssetPath, assetPath.AssetPath)),
            ResolveProjectAssetPathSelectorInput projectAssetPath => CreateArgs(
                (IpcResolveSelectorPropertyNames.ProjectAssetPath, projectAssetPath.ProjectAssetPath)),
            ResolveSceneHierarchySelectorInput sceneHierarchy => CreateArgs(
                (IpcResolveSelectorPropertyNames.Scene, sceneHierarchy.Scene),
                (IpcResolveSelectorPropertyNames.HierarchyPath, sceneHierarchy.HierarchyPath)),
            ResolveSceneComponentSelectorInput sceneComponent => CreateArgs(
                (IpcResolveSelectorPropertyNames.Scene, sceneComponent.Scene),
                (IpcResolveSelectorPropertyNames.HierarchyPath, sceneComponent.HierarchyPath),
                (IpcResolveSelectorPropertyNames.ComponentType, sceneComponent.ComponentType)),
            ResolvePrefabHierarchySelectorInput prefabHierarchy => CreateArgs(
                (IpcResolveSelectorPropertyNames.Prefab, prefabHierarchy.Prefab),
                (IpcResolveSelectorPropertyNames.HierarchyPath, prefabHierarchy.HierarchyPath)),
            _ => throw new ArgumentException("Unsupported resolve selector type.", nameof(selector)),
        };
    }

    private static JsonElement CreateArgs (params (string Name, string Value)[] properties)
    {
        var args = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            args.Add(property.Name, property.Value);
        }

        return IpcPayloadCodec.SerializeToElement(args);
    }
}
