using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("GameObject describe operation arguments.")]
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

    [JsonInclude]
    [JsonRequired]
    [Description("Target GameObject reference.")]
    [UcliReferenceResolvable(UcliOperationReferenceTargetKind.GameObject)]
    public GameObjectReferenceArgs Target { get; private init; }

    [Description("Maximum child hierarchy depth to include; null means unbounded.")]
    [UcliInt32Minimum(0)]
    public int? Depth { get; }
}
