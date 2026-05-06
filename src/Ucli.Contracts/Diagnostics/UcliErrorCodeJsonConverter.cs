using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts;

/// <summary> Converts <see cref="UcliErrorCode" /> values as JSON strings. </summary>
public sealed class UcliErrorCodeJsonConverter : JsonConverter<UcliErrorCode>
{
    /// <inheritdoc />
    public override UcliErrorCode Read (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a JSON string for an error code.");
        }

        var value = reader.GetString();
        try
        {
            return new UcliErrorCode(value!);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("JSON string for an error code is invalid.", exception);
        }
    }

    /// <inheritdoc />
    public override void Write (
        Utf8JsonWriter writer,
        UcliErrorCode value,
        JsonSerializerOptions options)
    {
        if (!value.IsValid)
        {
            throw new JsonException("Error code value is invalid.");
        }

        writer.WriteStringValue(value.Value);
    }
}
