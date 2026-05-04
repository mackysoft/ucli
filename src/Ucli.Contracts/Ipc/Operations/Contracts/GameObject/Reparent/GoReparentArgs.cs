using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("GameObject reparent operation arguments.")]
public sealed record GoReparentArgs
{
    [JsonConstructor]
    public GoReparentArgs (
        GameObjectReferenceArgs target,
        GameObjectReferenceArgs parent)
    {
        Target = target;
        Parent = parent;
    }

    [UcliRequired]
    [UcliDescription("Target GameObject reference.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.GameObject)]
    public GameObjectReferenceArgs Target { get; init; }

    [UcliRequired]
    [UcliDescription("New parent GameObject reference.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.GameObject)]
    public GameObjectReferenceArgs Parent { get; init; }
}
