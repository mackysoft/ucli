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

        ResolveSelectorArgs args = selector switch
        {
            ResolveGlobalObjectIdSelectorInput globalObjectId =>
                new GlobalObjectIdReferenceArgs(globalObjectId.GlobalObjectId),
            ResolveAssetGuidSelectorInput assetGuid =>
                new AssetGuidReferenceArgs(assetGuid.AssetGuid),
            ResolveAssetPathSelectorInput assetPath =>
                new AssetPathReferenceArgs(assetPath.AssetPath),
            ResolveProjectAssetPathSelectorInput projectAssetPath =>
                new ProjectAssetPathReferenceArgs(projectAssetPath.ProjectAssetPath),
            ResolveSceneHierarchySelectorInput sceneHierarchy =>
                new SceneHierarchyReferenceArgs(sceneHierarchy.Scene, sceneHierarchy.HierarchyPath),
            ResolveSceneComponentSelectorInput sceneComponent =>
                new SceneComponentReferenceArgs(
                    sceneComponent.Scene,
                    sceneComponent.HierarchyPath,
                    sceneComponent.ComponentType),
            ResolvePrefabHierarchySelectorInput prefabHierarchy =>
                new PrefabHierarchyReferenceArgs(prefabHierarchy.Prefab, prefabHierarchy.HierarchyPath),
            _ => throw new ArgumentException("Unsupported resolve selector type.", nameof(selector)),
        };

        return JsonSerializer.SerializeToElement<ResolveSelectorArgs>(args, IpcJsonSerializerOptions.Default);
    }
}
