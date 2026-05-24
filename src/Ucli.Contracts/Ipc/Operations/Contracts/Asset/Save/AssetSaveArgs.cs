using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset save operation arguments.")]
public sealed record AssetSaveArgs
{
    [JsonConstructor]
    public AssetSaveArgs (AssetReferenceArgs target)
    {
        Target = target;
    }

    [UcliRequired]
    [UcliDescription("Target asset or ProjectSettings asset to save.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.Asset)]
    public AssetReferenceArgs Target { get; init; }
}
