using System.Text.Json.Serialization;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Component type operation arguments.")]
public sealed record ComponentTypeArgs
{
    [JsonConstructor]
    public ComponentTypeArgs (UnityComponentTypeId type)
    {
        Type = ContractArgumentGuard.RequireNotNull(type, nameof(type));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Unity type identifier assignable to a Component type.")]
    public UnityComponentTypeId Type { get; private init; }
}
