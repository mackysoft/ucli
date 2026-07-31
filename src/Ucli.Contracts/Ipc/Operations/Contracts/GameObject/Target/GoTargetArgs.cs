using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("GameObject target operation arguments.")]
public sealed record GoTargetArgs
{
    [JsonConstructor]
    public GoTargetArgs (GameObjectReferenceArgs target)
    {
        Target = ContractArgumentGuard.RequireNotNull(target, nameof(target));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Target GameObject reference.")]
    [UcliReferenceResolvable(UcliOperationReferenceTargetKind.GameObject)]
    public GameObjectReferenceArgs Target { get; private init; }
}
