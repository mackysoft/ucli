using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval return value.")]
public sealed record CsEvalReturnValue
{
    [JsonConstructor]
    public CsEvalReturnValue (
        string kind,
        JsonElement? value)
    {
        Kind = kind;
        Value = value;
    }

    [UcliRequired]
    [UcliDescription("Return value kind literal: void, null, or json.")]
    public string Kind { get; init; }

    [UcliDescription("JSON return value when kind is json.")]
    [UcliJsonAnyValue]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Value { get; init; }
}
