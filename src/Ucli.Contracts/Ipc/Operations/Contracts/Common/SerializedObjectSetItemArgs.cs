using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Serialized object set item.")]
public sealed record SerializedObjectSetItemArgs
{
    [JsonConstructor]
    public SerializedObjectSetItemArgs (
        SerializedPropertyPath path,
        JsonElement value)
    {
        Path = path;
        Value = value;
    }

    public SerializedObjectSetItemArgs (
        string path,
        JsonElement value)
        : this(new SerializedPropertyPath(path), value)
    {
    }

    [UcliRequired]
    [UcliDescription("SerializedProperty path to assign.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.SerializedProperty, Access = UcliOperationSerializedPropertyAccess.Write)]
    public SerializedPropertyPath Path { get; init; }

    [UcliRequired]
    [UcliDescription("JSON value assigned to the serialized property.")]
    [UcliJsonAnyValue]
    public JsonElement Value { get; init; }
}
