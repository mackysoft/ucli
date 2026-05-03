using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset search operation arguments.")]
public sealed record AssetsFindArgs
{
    [JsonConstructor]
    public AssetsFindArgs (
        string? type,
        string? pathPrefix,
        string? nameContains)
    {
        Type = type;
        PathPrefix = pathPrefix;
        NameContains = nameContains;
    }

    [UcliDescription("Optional asset type identifier filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; init; }

    [UcliDescription("Optional asset path prefix filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PathPrefix { get; init; }

    [UcliDescription("Optional case-sensitive asset name substring filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NameContains { get; init; }
}
