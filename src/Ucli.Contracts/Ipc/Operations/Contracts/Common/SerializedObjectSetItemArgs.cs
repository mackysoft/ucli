using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Serialized object set item.")]
public sealed record SerializedObjectSetItemArgs
{
    [JsonConstructor]
    public SerializedObjectSetItemArgs (
        SerializedPropertyPath path,
        JsonElement value)
    {
        Path = ContractArgumentGuard.RequireNotNull(path, nameof(path));
        Value = value;
    }

    [JsonInclude]
    [JsonRequired]
    [Description("SerializedProperty path to assign.")]
    [UcliSerializedProperty(UcliOperationSerializedPropertyAccess.Write)]
    public SerializedPropertyPath Path { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("JSON value assigned to the serialized property.")]
    public JsonElement Value { get; private init; }
}
