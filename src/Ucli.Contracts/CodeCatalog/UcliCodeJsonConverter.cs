using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts;

/// <summary> Converts <see cref="UcliCode" /> values as JSON strings. </summary>
public sealed class UcliCodeJsonConverter : JsonConverter<UcliCode>
{
    /// <inheritdoc />
    public override UcliCode Read (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a JSON string for a code value.");
        }

        if (!UcliCode.TryCreate(reader.GetString(), out var code))
        {
            throw new JsonException("JSON string for a code value is invalid.");
        }

        return code;
    }

    /// <inheritdoc />
    public override void Write (
        Utf8JsonWriter writer,
        UcliCode value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
