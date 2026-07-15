using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Component property set operation arguments.")]
public sealed record ComponentSetArgs
{
    [JsonConstructor]
    public ComponentSetArgs (
        ComponentReferenceArgs target,
        IReadOnlyList<SerializedObjectSetItemArgs> sets)
    {
        Target = ContractArgumentGuard.RequireNotNull(target, nameof(target));
        Sets = ContractArgumentGuard.RequireItems(sets, nameof(sets));
    }

    [UcliRequired]
    [UcliDescription("Target component to modify.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.Component)]
    public ComponentReferenceArgs Target { get; }

    [UcliRequired]
    [UcliDescription("Serialized property assignments.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    [UcliInputConstraint(UcliOperationInputConstraintKind.SerializedProperty, Access = UcliOperationSerializedPropertyAccess.Write)]
    public IReadOnlyList<SerializedObjectSetItemArgs> Sets { get; }
}
