using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Component property set operation arguments.")]
public sealed record ComponentSetArgs
{
    [JsonConstructor]
    public ComponentSetArgs (
        ComponentReferenceArgs target,
        IReadOnlyList<SerializedObjectSetItemArgs> sets)
    {
        Target = target;
        Sets = sets;
    }

    [UcliRequired]
    [UcliDescription("Target component to modify.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.Component)]
    public ComponentReferenceArgs Target { get; init; }

    [UcliRequired]
    [UcliDescription("Serialized property assignments.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    [UcliInputConstraint(UcliOperationInputConstraintKind.SerializedProperty, Access = UcliOperationSerializedPropertyAccess.Write)]
    public IReadOnlyList<SerializedObjectSetItemArgs> Sets { get; init; }
}
