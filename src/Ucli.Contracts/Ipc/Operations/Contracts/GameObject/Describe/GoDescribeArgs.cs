using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("GameObject describe operation arguments.")]
public sealed record GoDescribeArgs
{
    [JsonConstructor]
    public GoDescribeArgs (
        GameObjectReferenceArgs target,
        int? depth)
    {
        Target = target;
        Depth = depth;
    }

    [UcliRequired]
    [UcliDescription("Target GameObject reference.")]
    public GameObjectReferenceArgs Target { get; init; }

    [UcliDescription("Maximum child hierarchy depth to include; null means unbounded.")]
    public int? Depth { get; init; }
}
