using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset property set operation arguments.")]
public sealed record AssetSetArgs
{
    [JsonConstructor]
    public AssetSetArgs (
        AssetReferenceArgs target,
        IReadOnlyList<SerializedObjectSetItemArgs> sets)
    {
        Target = target;
        Sets = sets;
    }

    [UcliRequired]
    [UcliDescription("Target asset to modify.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.Asset)]
    public AssetReferenceArgs Target { get; init; }

    [UcliRequired]
    [UcliDescription("Serialized property assignments.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    [UcliInputConstraint(UcliOperationInputConstraintKind.SerializedProperty, Access = UcliOperationSerializedPropertyAccess.Write)]
    public IReadOnlyList<SerializedObjectSetItemArgs> Sets { get; init; }
}
