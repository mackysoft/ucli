using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset search operation arguments. Specify at least one filter. Searches persistent main assets under Assets.")]
public sealed record AssetsFindArgs
{
    [JsonConstructor]
    public AssetsFindArgs (
        UnityTypeId? type,
        ProjectRelativePathPrefix? pathPrefix,
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

    public AssetsFindArgs (
        UnityTypeId? type,
        ProjectRelativePathPrefix? pathPrefix,
        string? nameContains)
        : this(type, pathPrefix, nameContains, limit: null, cursor: null)
    {
    }

    public AssetsFindArgs (
        string? type,
        string? pathPrefix,
        string? nameContains)
        : this(type, pathPrefix, nameContains, limit: null, cursor: null)
    {
    }

    public AssetsFindArgs (
        string? type,
        string? pathPrefix,
        string? nameContains,
        int? limit,
        string? cursor)
        : this(
            type == null ? null : new UnityTypeId(type),
            pathPrefix == null ? null : new ProjectRelativePathPrefix(pathPrefix),
            nameContains,
            limit,
            cursor)
    {
    }

    [UcliDescription("Optional asset type identifier filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityTypeId? Type { get; init; }

    [UcliDescription("Optional project-relative Assets path prefix filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectRelativePathPrefix? PathPrefix { get; init; }

    [UcliDescription("Optional case-insensitive asset name substring filter.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NameContains { get; init; }

    [UcliDescription("Maximum number of matches to include in the response window.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.Range, Min = 1, Max = BoundedWindowConstants.MaxLimit)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Limit { get; init; }

    [UcliDescription("Opaque cursor returned by the previous assets-find window.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.Cursor)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cursor { get; init; }
}
