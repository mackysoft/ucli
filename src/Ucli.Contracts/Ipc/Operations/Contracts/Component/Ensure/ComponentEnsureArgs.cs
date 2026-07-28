using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Component ensure operation arguments.")]
public sealed record ComponentEnsureArgs
{
    [JsonConstructor]
    public ComponentEnsureArgs (
        GameObjectReferenceArgs target,
        UnityComponentTypeId type)
    {
        Target = ContractArgumentGuard.RequireNotNull(target, nameof(target));
        Type = ContractArgumentGuard.RequireNotNull(type, nameof(type));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Target GameObject that should contain the component.")]
    [UcliReferenceResolvable(UcliOperationReferenceTargetKind.GameObject)]
    public GameObjectReferenceArgs Target { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Component type identifier to ensure.")]
    public UnityComponentTypeId Type { get; private init; }
}
