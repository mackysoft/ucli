using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Projection;

/// <summary> Creates <c>ucli.resolve</c> operation argument payloads from normalized selectors. </summary>
internal static class ResolveSelectorOperationArgsFactory
{
    /// <summary> Creates the JSON object used as one <c>ucli.resolve</c> operation args payload. </summary>
    public static JsonElement Create (ResolveSelectorInput selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var args = selector switch
        {
            ResolveGlobalObjectIdSelectorInput globalObjectId => new ResolveSelectorArgs(
                globalObjectId: globalObjectId.GlobalObjectId,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                scene: null,
                prefab: null,
                hierarchyPath: null,
                componentType: null),
            ResolveAssetGuidSelectorInput assetGuid => new ResolveSelectorArgs(
                globalObjectId: null,
                assetGuid: assetGuid.AssetGuid,
                assetPath: null,
                projectAssetPath: null,
                scene: null,
                prefab: null,
                hierarchyPath: null,
                componentType: null),
            ResolveAssetPathSelectorInput assetPath => new ResolveSelectorArgs(
                globalObjectId: null,
                assetGuid: null,
                assetPath: new UnityAssetPath(assetPath.AssetPath),
                projectAssetPath: null,
                scene: null,
                prefab: null,
                hierarchyPath: null,
                componentType: null),
            ResolveProjectAssetPathSelectorInput projectAssetPath => new ResolveSelectorArgs(
                globalObjectId: null,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: new ProjectSettingsAssetPath(projectAssetPath.ProjectAssetPath),
                scene: null,
                prefab: null,
                hierarchyPath: null,
                componentType: null),
            ResolveSceneHierarchySelectorInput sceneHierarchy => new ResolveSelectorArgs(
                globalObjectId: null,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                scene: new SceneAssetPath(sceneHierarchy.Scene),
                prefab: null,
                hierarchyPath: new UnityHierarchyPath(sceneHierarchy.HierarchyPath),
                componentType: null),
            ResolveSceneComponentSelectorInput sceneComponent => new ResolveSelectorArgs(
                globalObjectId: null,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                scene: new SceneAssetPath(sceneComponent.Scene),
                prefab: null,
                hierarchyPath: new UnityHierarchyPath(sceneComponent.HierarchyPath),
                componentType: new UnityComponentTypeId(sceneComponent.ComponentType)),
            ResolvePrefabHierarchySelectorInput prefabHierarchy => new ResolveSelectorArgs(
                globalObjectId: null,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                scene: null,
                prefab: new PrefabAssetPath(prefabHierarchy.Prefab),
                hierarchyPath: new UnityHierarchyPath(prefabHierarchy.HierarchyPath),
                componentType: null),
            _ => throw new ArgumentException("Unsupported resolve selector type.", nameof(selector)),
        };

        return JsonSerializer.SerializeToElement(args, IpcJsonSerializerOptions.Default);
    }
}
