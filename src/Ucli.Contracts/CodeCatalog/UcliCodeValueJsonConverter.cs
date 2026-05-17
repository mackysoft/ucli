using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts;

/// <summary> Converts <see cref="UcliCodeValue" /> values as JSON strings. </summary>
public sealed class UcliCodeValueJsonConverter : JsonConverter<UcliCodeValue>
{
    /// <inheritdoc />
    public override UcliCodeValue Read (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a JSON string for a code value.");
        }

        var value = reader.GetString();
        try
        {
            return new UcliCodeValue(value!);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("JSON string for a code value is invalid.", exception);
        }
    }

    /// <inheritdoc />
    public override void Write (
        Utf8JsonWriter writer,
        UcliCodeValue value,
        JsonSerializerOptions options)
    {
        if (!value.IsValid)
        {
            throw new JsonException("Code value is invalid.");
        }

        writer.WriteStringValue(value.Value);
    }
}
