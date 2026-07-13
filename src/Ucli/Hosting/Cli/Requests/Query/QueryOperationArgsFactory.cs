using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates primitive operation argument payloads for typed query commands. </summary>
internal static class QueryOperationArgsFactory
{
    /// <summary> Creates <c>ucli.go.describe</c> args. </summary>
    public static JsonElement CreateGoDescribe (
        IReadOnlyDictionary<string, string> target,
        int? depth)
    {
        ArgumentNullException.ThrowIfNull(target);
        return IpcPayloadCodec.SerializeToElement(new GoDescribeArgs(
            target: CreateGameObjectReference(target),
            depth: depth));
    }

    /// <summary> Creates <c>ucli.comp.schema</c> args. </summary>
    public static JsonElement CreateCompSchema (string typeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        return IpcPayloadCodec.SerializeToElement(new ComponentTypeArgs(new UnityComponentTypeId(typeId)));
    }

    /// <summary> Creates <c>ucli.asset.schema</c> args for a type selector. </summary>
    public static JsonElement CreateAssetSchemaType (string typeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        return IpcPayloadCodec.SerializeToElement(new AssetSchemaArgs(
            type: new UnityTypeId(typeId),
            target: null));
    }

    /// <summary> Creates <c>ucli.asset.schema</c> args for a target selector. </summary>
    public static JsonElement CreateAssetSchemaTarget (IReadOnlyDictionary<string, string> target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return IpcPayloadCodec.SerializeToElement(new AssetSchemaArgs(
            type: null,
            target: CreateAssetReference(target)));
    }

    private static GameObjectReferenceArgs CreateGameObjectReference (
        IReadOnlyDictionary<string, string> target)
    {
        var globalObjectId = GetValueOrNull(target, "globalObjectId");
        var prefab = GetValueOrNull(target, "prefab");
        var scene = GetValueOrNull(target, "scene");
        var hierarchyPath = GetValueOrNull(target, "hierarchyPath");
        return new GameObjectReferenceArgs(
            alias: null,
            globalObjectId: globalObjectId == null ? null : new UnityGlobalObjectId(globalObjectId),
            prefab: prefab == null ? null : new PrefabAssetPath(prefab),
            scene: scene == null ? null : new SceneAssetPath(scene),
            hierarchyPath: hierarchyPath == null ? null : new UnityHierarchyPath(hierarchyPath));
    }

    private static AssetReferenceArgs CreateAssetReference (
        IReadOnlyDictionary<string, string> target)
    {
        var globalObjectId = GetValueOrNull(target, "globalObjectId");
        var assetGuid = GetValueOrNull(target, "assetGuid");
        var assetPath = GetValueOrNull(target, "assetPath");
        var projectAssetPath = GetValueOrNull(target, "projectAssetPath");
        return new AssetReferenceArgs(
            alias: null,
            globalObjectId: globalObjectId == null ? null : new UnityGlobalObjectId(globalObjectId),
            assetGuid: assetGuid == null ? null : new UnityAssetGuid(assetGuid),
            assetPath: assetPath == null ? null : new UnityAssetPath(assetPath),
            projectAssetPath: projectAssetPath == null ? null : new ProjectSettingsAssetPath(projectAssetPath));
    }

    private static string? GetValueOrNull (
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }
}
