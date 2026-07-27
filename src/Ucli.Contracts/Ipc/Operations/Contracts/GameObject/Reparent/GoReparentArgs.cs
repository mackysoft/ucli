using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("GameObject reparent operation arguments.")]
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

    [JsonInclude]
    [JsonRequired]
    [Description("Target GameObject reference.")]
    [UcliReferenceResolvable(UcliOperationReferenceTargetKind.GameObject)]
    public GameObjectReferenceArgs Target { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("New parent GameObject reference.")]
    [UcliReferenceResolvable(UcliOperationReferenceTargetKind.GameObject)]
    public GameObjectReferenceArgs Parent { get; private init; }
}
