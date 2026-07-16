using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Serialized object set item.")]
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

    [UcliRequired]
    [UcliDescription("SerializedProperty path to assign.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.SerializedProperty, Access = UcliOperationSerializedPropertyAccess.Write)]
    public SerializedPropertyPath Path { get; }

    [UcliRequired]
    [UcliDescription("JSON value assigned to the serialized property.")]
    [UcliJsonAnyValue]
    public JsonElement Value { get; }
}
