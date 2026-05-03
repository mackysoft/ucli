using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Component ensure operation arguments.")]
public sealed record ComponentEnsureArgs
{
    [JsonConstructor]
    public ComponentEnsureArgs (
        GameObjectReferenceArgs target,
        string type)
    {
        Target = target;
        Type = type;
    }

    [UcliRequired]
    [UcliDescription("Target GameObject that should contain the component.")]
    public GameObjectReferenceArgs Target { get; init; }

    [UcliRequired]
    [UcliDescription("Component type identifier to ensure.")]
    public string Type { get; init; }
}
