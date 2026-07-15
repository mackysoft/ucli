using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Reads IPC timestamps only when their JSON literals contain explicit timezone information. </summary>
internal sealed class IpcTimestampJsonConverter : JsonConverter<DateTimeOffset>
{
    /// <inheritdoc />
    public override DateTimeOffset Read (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("IPC timestamp must be a JSON string.");
        }

        var timestampLiteral = reader.GetString();
        if (!IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(timestampLiteral, out var timestamp)
            || !timestamp.HasValue)
        {
            throw new JsonException("IPC timestamp must be an ISO 8601 value with an explicit timezone offset.");
        }

        return timestamp.Value;
    }

    /// <inheritdoc />
    public override void Write (
        Utf8JsonWriter writer,
        DateTimeOffset value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
