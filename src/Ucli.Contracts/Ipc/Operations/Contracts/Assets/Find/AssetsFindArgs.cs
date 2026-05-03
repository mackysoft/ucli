using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset search operation arguments.")]
public sealed record AssetsFindArgs
{
    [JsonConstructor]
    public AssetsFindArgs (
        UnityTypeId? type,
        ProjectRelativePathPrefix? pathPrefix,
        string? nameContains)
    {
        Type = type;
        PathPrefix = pathPrefix;
        NameContains = nameContains;
    }

    public AssetsFindArgs (
        string? type,
        string? pathPrefix,
        string? nameContains)
        : this(
            type == null ? null : new UnityTypeId(type),
            pathPrefix == null ? null : new ProjectRelativePathPrefix(pathPrefix),
            nameContains)
    {
    }

    [UcliDescription("Optional asset type identifier filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityTypeId? Type { get; init; }

    [UcliDescription("Optional asset path prefix filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectRelativePathPrefix? PathPrefix { get; init; }

    [UcliDescription("Optional case-sensitive asset name substring filter.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NameContains { get; init; }
}
