using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("GameObject target operation arguments.")]
public sealed record GoTargetArgs
{
    [JsonConstructor]
    public GoTargetArgs (GameObjectReferenceArgs target)
    {
        Target = target;
    }

    [UcliRequired]
    [UcliDescription("Target GameObject reference.")]
    public GameObjectReferenceArgs Target { get; init; }
}
