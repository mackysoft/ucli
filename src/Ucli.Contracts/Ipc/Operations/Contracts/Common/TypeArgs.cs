using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Single Unity type operation arguments.")]
public sealed record TypeArgs
{
    [JsonConstructor]
    public TypeArgs (string type)
    {
        Type = type;
    }

    [UcliRequired]
    [UcliDescription("Assembly-qualified or otherwise resolvable Unity type identifier.")]
    [UcliMinLength(1)]
    public string Type { get; init; }
}
