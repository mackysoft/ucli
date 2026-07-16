using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset schema operation arguments.")]
[UcliExclusiveRequiredPropertySet("type")]
[UcliExclusiveRequiredPropertySet("target")]
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
    public UnityTypeId? Type { get; }

    [UcliDescription("Existing asset target to inspect.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.Asset)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssetReferenceArgs? Target { get; }
}
