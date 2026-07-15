using System.Buffers;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Serialization;

namespace MackySoft.Ucli.Hosting.Cli.Common.Streaming;

/// <summary> Writes public stream entries to one configured text stream. </summary>
internal sealed class CliStreamEntryWriter
{
    private readonly string command;
    private readonly Guid streamId;
    private readonly TextWriter errorWriter;
    private readonly TimeProvider timeProvider;
    private readonly ArrayBufferWriter<byte> jsonBuffer = new();

    private long sequence;

    /// <summary> Initializes a new instance of the <see cref="CliStreamEntryWriter" /> class. </summary>
    public CliStreamEntryWriter (
        string command,
        Guid streamId,
        TextWriter errorWriter,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (streamId == Guid.Empty)
        {
            throw new ArgumentException("Stream identifier must not be empty.", nameof(streamId));
        }

        this.command = command;
        this.streamId = streamId;
        this.errorWriter = errorWriter ?? throw new ArgumentNullException(nameof(errorWriter));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Writes one JSON entry envelope as one NDJSON line. </summary>
    /// <typeparam name="TPayload"> The concrete event payload type. </typeparam>
    public void WriteJsonEntry<TPayload> (
        string eventName,
        TPayload payload)
        where TPayload : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        jsonBuffer.Clear();
        using (var writer = new Utf8JsonWriter(jsonBuffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("protocolVersion", IpcProtocol.CurrentVersion);
            writer.WriteString("command", command);
            writer.WriteString("streamId", streamId);
            writer.WriteNumber("sequence", checked(++sequence));
            writer.WriteString("timestamp", timeProvider.GetUtcNow().ToString("O"));
            writer.WriteString("event", eventName);
            writer.WritePropertyName("payload");
            JsonSerializer.Serialize(writer, payload, CliOutputJsonSerializerOptions.Default);
            writer.WriteEndObject();
        }

        WriteUtf8JsonLine(jsonBuffer.WrittenSpan);
    }

    /// <summary> Writes one human-readable text entry line. </summary>
    public void WriteTextEntry (string text)
    {
        errorWriter.WriteLine(CliTextEntrySanitizer.Sanitize(text));
    }

    private void WriteUtf8JsonLine (ReadOnlySpan<byte> utf8Json)
    {
        var maxCharCount = Encoding.UTF8.GetMaxCharCount(utf8Json.Length);
        var charBuffer = ArrayPool<char>.Shared.Rent(maxCharCount);
        try
        {
            var charCount = Encoding.UTF8.GetChars(utf8Json, charBuffer);
            errorWriter.Write(charBuffer.AsSpan(0, charCount));
            errorWriter.WriteLine();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(charBuffer);
        }
    }
}
