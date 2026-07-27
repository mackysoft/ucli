using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Asset search operation arguments. Specify at least one filter. Searches persistent main assets under Assets.")]
public sealed record AssetsFindArgs
{
    [JsonConstructor]
    public AssetsFindArgs (
        UnityTypeId? type,
        UnityAssetPathPrefix? pathPrefix,
        string? nameContains,
        int? limit,
        string? cursor)
    {
        Type = type;
        PathPrefix = pathPrefix;
        NameContains = nameContains;
        Limit = limit;
        Cursor = cursor;
    }

    [Description("Optional asset type identifier filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityTypeId? Type { get; }

    [Description("Optional project-relative Assets path prefix filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityAssetPathPrefix? PathPrefix { get; }

    [Description("Optional case-insensitive asset name substring filter.")]
    [Length(1, int.MaxValue)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NameContains { get; }

    [Description("Maximum number of matches to include in the response window.")]
    [UcliInt32Range(1, BoundedWindowConstants.MaxLimit)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Limit { get; }

    [Description("Opaque cursor returned by the previous assets-find window.")]
    [UcliCursor]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cursor { get; }
}
