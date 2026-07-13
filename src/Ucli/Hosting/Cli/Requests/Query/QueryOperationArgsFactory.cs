using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates primitive operation argument payloads for typed query commands. </summary>
internal static class QueryOperationArgsFactory
{
    /// <summary> Creates <c>ucli.go.describe</c> args. </summary>
    public static JsonElement CreateGoDescribe (
        GameObjectReferenceArgs target,
        int? depth)
    {
        ArgumentNullException.ThrowIfNull(target);
        return IpcPayloadCodec.SerializeToElement(new GoDescribeArgs(
            target,
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
    public static JsonElement CreateAssetSchemaTarget (AssetReferenceArgs target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return IpcPayloadCodec.SerializeToElement(new AssetSchemaArgs(
            type: null,
            target));
    }
}
