using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Cryptography;

/// <summary> Converts <see cref="Sha256Digest" /> values as canonical lowercase hexadecimal JSON strings. </summary>
public sealed class Sha256DigestJsonConverter : JsonConverter<Sha256Digest>
{
    /// <inheritdoc />
    public override Sha256Digest Read (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a JSON string for a SHA-256 digest.");
        }

        if (!Sha256Digest.TryParse(reader.GetString(), out var digest))
        {
            throw new JsonException("JSON string for a SHA-256 digest is invalid.");
        }

        return digest;
    }

    /// <inheritdoc />
    public override void Write (
        Utf8JsonWriter writer,
        Sha256Digest value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
