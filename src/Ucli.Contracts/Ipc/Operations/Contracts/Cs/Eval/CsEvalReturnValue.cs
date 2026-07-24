using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval return value.")]
public sealed record CsEvalReturnValue
{
    [JsonConstructor]
    public CsEvalReturnValue (
        CsEvalReturnValueKind kind,
        JsonElement? value)
    {
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "C# eval return value kind must be specified.");
        }

        if ((kind == CsEvalReturnValueKind.Null) != !value.HasValue)
        {
            throw new ArgumentException("C# eval return value must be omitted exactly when kind is null.", nameof(value));
        }

        Kind = kind;
        Value = value;
    }

    [UcliRequired]
    [UcliDescription("Return value representation.")]
    public CsEvalReturnValueKind Kind { get; }

    [UcliDescription("JSON return value when kind is json.")]
    [UcliJsonAnyValue]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Value { get; }
}
