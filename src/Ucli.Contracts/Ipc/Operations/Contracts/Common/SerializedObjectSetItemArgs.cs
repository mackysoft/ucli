using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Serialized object set item.")]
public sealed record SerializedObjectSetItemArgs
{
    [JsonConstructor]
    public SerializedObjectSetItemArgs (
        string path,
        JsonElement value)
    {
        Path = path;
        Value = value;
    }

    [UcliRequired]
    [UcliDescription("SerializedProperty path to assign.")]
    [UcliMinLength(1)]
    public string Path { get; init; }

    [UcliRequired]
    [UcliDescription("JSON value assigned to the serialized property.")]
    [UcliSchemaAny]
    public JsonElement Value { get; init; }
}
