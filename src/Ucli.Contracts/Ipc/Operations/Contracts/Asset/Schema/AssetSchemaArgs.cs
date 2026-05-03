using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset schema operation arguments.")]
[UcliRequiredPropertyAlternative("type")]
[UcliRequiredPropertyAlternative("target")]
public sealed record AssetSchemaArgs
{
    [JsonConstructor]
    public AssetSchemaArgs (
        UnityTypeId? type,
        AssetReferenceArgs? target)
    {
        Type = type;
        Target = target;
    }

    [UcliDescription("Unity asset type identifier to inspect.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnityTypeId? Type { get; init; }

    [UcliDescription("Existing asset target to inspect.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.Asset)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssetReferenceArgs? Target { get; init; }
}
