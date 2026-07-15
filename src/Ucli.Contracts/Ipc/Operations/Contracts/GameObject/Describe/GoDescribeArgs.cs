using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("GameObject describe operation arguments.")]
public sealed record GoDescribeArgs
{
    [JsonConstructor]
    public GoDescribeArgs (
        GameObjectReferenceArgs target,
        int? depth)
    {
        Target = ContractArgumentGuard.RequireNotNull(target, nameof(target));
        Depth = depth;
    }

    [UcliRequired]
    [UcliDescription("Target GameObject reference.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.GameObject)]
    public GameObjectReferenceArgs Target { get; }

    [UcliDescription("Maximum child hierarchy depth to include; null means unbounded.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.Range, Min = 0)]
    public int? Depth { get; }
}
