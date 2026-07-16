using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset search operation arguments. Specify at least one filter. Searches persistent main assets under Assets.")]
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

    [UcliDescription("Optional asset type identifier filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityTypeId? Type { get; }

    [UcliDescription("Optional project-relative Assets path prefix filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityAssetPathPrefix? PathPrefix { get; }

    [UcliDescription("Optional case-insensitive asset name substring filter.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NameContains { get; }

    [UcliDescription("Maximum number of matches to include in the response window.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.Range, Min = 1, Max = BoundedWindowConstants.MaxLimit)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Limit { get; }

    [UcliDescription("Opaque cursor returned by the previous assets-find window.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.Cursor)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cursor { get; }
}
