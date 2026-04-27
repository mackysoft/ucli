using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets.Access;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates primitive operation argument payloads for typed query commands. </summary>
internal static class QueryOperationArgsFactory
{
    /// <summary> Creates <c>ucli.assets.find</c> args. </summary>
    public static JsonElement CreateAssetsFind (AssetSearchLookupQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var args = new Dictionary<string, string>(StringComparer.Ordinal);
        AddIfNotNull(args, "type", query.TypeId);
        AddIfNotNull(args, "pathPrefix", query.PathPrefix);
        AddIfNotNull(args, "nameContains", query.NameContains);
        return IpcPayloadCodec.SerializeToElement(args);
    }

    /// <summary> Creates <c>ucli.scene.tree</c> args. </summary>
    public static JsonElement CreateSceneTree (
        string scenePath,
        int? depth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        return IpcPayloadCodec.SerializeToElement(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["path"] = scenePath,
            ["depth"] = depth,
        });
    }

    /// <summary> Creates <c>ucli.go.describe</c> args. </summary>
    public static JsonElement CreateGoDescribe (
        IReadOnlyDictionary<string, string> target,
        int? depth)
    {
        ArgumentNullException.ThrowIfNull(target);
        return IpcPayloadCodec.SerializeToElement(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["target"] = target,
            ["depth"] = depth,
        });
    }

    /// <summary> Creates <c>ucli.comp.schema</c> args. </summary>
    public static JsonElement CreateCompSchema (string typeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        return IpcPayloadCodec.SerializeToElement(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["type"] = typeId,
        });
    }

    /// <summary> Creates <c>ucli.asset.schema</c> args for a type selector. </summary>
    public static JsonElement CreateAssetSchemaType (string typeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        return IpcPayloadCodec.SerializeToElement(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["type"] = typeId,
        });
    }

    /// <summary> Creates <c>ucli.asset.schema</c> args for a target selector. </summary>
    public static JsonElement CreateAssetSchemaTarget (IReadOnlyDictionary<string, string> target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return IpcPayloadCodec.SerializeToElement(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["target"] = target,
        });
    }

    private static void AddIfNotNull (
        IDictionary<string, string> args,
        string name,
        string? value)
    {
        if (value is null)
        {
            return;
        }

        args.Add(name, value);
    }
}
