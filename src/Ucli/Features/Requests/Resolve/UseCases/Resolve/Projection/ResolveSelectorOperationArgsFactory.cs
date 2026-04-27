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

        var args = new Dictionary<string, string>(StringComparer.Ordinal);
        AddIfNotNull(args, IpcResolveSelectorPropertyNames.GlobalObjectId, selector.GlobalObjectId);
        AddIfNotNull(args, IpcResolveSelectorPropertyNames.AssetGuid, selector.AssetGuid);
        AddIfNotNull(args, IpcResolveSelectorPropertyNames.AssetPath, selector.AssetPath);
        AddIfNotNull(args, IpcResolveSelectorPropertyNames.ProjectAssetPath, selector.ProjectAssetPath);
        AddIfNotNull(args, IpcResolveSelectorPropertyNames.Scene, selector.Scene);
        AddIfNotNull(args, IpcResolveSelectorPropertyNames.HierarchyPath, selector.HierarchyPath);
        AddIfNotNull(args, IpcResolveSelectorPropertyNames.ComponentType, selector.ComponentType);
        AddIfNotNull(args, IpcResolveSelectorPropertyNames.Prefab, selector.Prefab);
        return IpcPayloadCodec.SerializeToElement(args);
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