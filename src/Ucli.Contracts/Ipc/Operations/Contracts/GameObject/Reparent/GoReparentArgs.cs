using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("GameObject reparent operation arguments.")]
public sealed record GoReparentArgs
{
    [JsonConstructor]
    public GoReparentArgs (
        GameObjectReferenceArgs target,
        GameObjectReferenceArgs parent)
    {
        Target = ContractArgumentGuard.RequireNotNull(target, nameof(target));
        Parent = ContractArgumentGuard.RequireNotNull(parent, nameof(parent));
    }

    [UcliRequired]
    [UcliDescription("Target GameObject reference.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.GameObject)]
    public GameObjectReferenceArgs Target { get; }

    [UcliRequired]
    [UcliDescription("New parent GameObject reference.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.GameObject)]
    public GameObjectReferenceArgs Parent { get; }
}
