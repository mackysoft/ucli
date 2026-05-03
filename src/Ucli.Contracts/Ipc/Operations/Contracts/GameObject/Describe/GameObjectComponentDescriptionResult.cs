using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Component entry in a GameObject description.")]
public sealed record GameObjectComponentDescriptionResult
{
    [JsonConstructor]
    public GameObjectComponentDescriptionResult (string? typeName)
    {
        TypeName = typeName;
    }

    [UcliDescription("Component type name, or null when the component script is missing.")]
    [UcliSchemaAllowNull]
    public string? TypeName { get; init; }
}
