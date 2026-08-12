using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CsEvalNullReturnValue), "null")]
[JsonDerivedType(typeof(CsEvalJsonReturnValue), "json")]
public abstract record CsEvalReturnValue
{
    public static CsEvalReturnValue Null () => new CsEvalNullReturnValue();

    public static CsEvalReturnValue Json (JsonElement value) => new CsEvalJsonReturnValue(value);
}

public sealed record CsEvalNullReturnValue : CsEvalReturnValue;

public sealed record CsEvalJsonReturnValue : CsEvalReturnValue
{
    [JsonConstructor]
    public CsEvalJsonReturnValue (JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("C# eval return value must be a JSON value.", nameof(value));
        }

        Value = value.Clone();
    }

    [JsonInclude]
    [JsonRequired]
    [Description("JSON return value.")]
    public JsonElement Value { get; private init; }
}
