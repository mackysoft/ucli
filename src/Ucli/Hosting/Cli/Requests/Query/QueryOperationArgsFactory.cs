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
        return IpcPayloadCodec.SerializeToElement(new TypeArgs(typeId));
    }

    /// <summary> Creates <c>ucli.asset.schema</c> args for a type selector. </summary>
    public static JsonElement CreateAssetSchemaType (string typeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        return IpcPayloadCodec.SerializeToElement(new AssetSchemaArgs(
            type: typeId,
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
        return new GameObjectReferenceArgs(
            alias: null,
            globalObjectId: GetValueOrNull(target, "globalObjectId"),
            prefab: GetValueOrNull(target, "prefab"),
            scene: GetValueOrNull(target, "scene"),
            hierarchyPath: GetValueOrNull(target, "hierarchyPath"));
    }

    private static AssetReferenceArgs CreateAssetReference (
        IReadOnlyDictionary<string, string> target)
    {
        return new AssetReferenceArgs(
            alias: null,
            globalObjectId: GetValueOrNull(target, "globalObjectId"),
            assetGuid: GetValueOrNull(target, "assetGuid"),
            assetPath: GetValueOrNull(target, "assetPath"),
            projectAssetPath: GetValueOrNull(target, "projectAssetPath"));
    }

    private static string? GetValueOrNull (
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }
}
