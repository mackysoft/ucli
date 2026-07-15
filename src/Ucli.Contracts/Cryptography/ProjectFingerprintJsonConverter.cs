using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts;

/// <summary> Converts <see cref="ProjectFingerprint" /> values as canonical JSON strings. </summary>
public sealed class ProjectFingerprintJsonConverter : JsonConverter<ProjectFingerprint>
{
    /// <inheritdoc />
    public override ProjectFingerprint Read (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a JSON string for a project fingerprint.");
        }

        if (!ProjectFingerprint.TryParse(reader.GetString(), out var fingerprint))
        {
            throw new JsonException("JSON string for a project fingerprint is invalid.");
        }

        return fingerprint;
    }

    /// <inheritdoc />
    public override void Write (
        Utf8JsonWriter writer,
        ProjectFingerprint value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
