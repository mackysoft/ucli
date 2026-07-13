using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Component type operation arguments.")]
public sealed record ComponentTypeArgs
{
    [JsonConstructor]
    public ComponentTypeArgs (UnityComponentTypeId type)
    {
        Type = type;
    }

    [UcliRequired]
    public UnityComponentTypeId Type { get; init; }
}
