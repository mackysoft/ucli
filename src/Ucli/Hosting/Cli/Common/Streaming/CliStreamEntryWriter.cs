using System.Buffers;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Hosting.Cli.Common.Streaming;

/// <summary> Writes public stream entries to standard error. </summary>
internal sealed class CliStreamEntryWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string command;
    private readonly string streamId;
    private readonly TextWriter errorWriter;
    private readonly TimeProvider timeProvider;
    private readonly ArrayBufferWriter<byte> jsonBuffer = new();

    private long sequence;

    /// <summary> Initializes a new instance of the <see cref="CliStreamEntryWriter" /> class. </summary>
    public CliStreamEntryWriter (
        string command,
        TextWriter? errorWriter = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        this.command = command;
        this.errorWriter = errorWriter ?? Console.Error;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        streamId = $"{command.Replace(' ', '.')}-{Guid.NewGuid():N}";
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
            JsonSerializer.Serialize(writer, payload, SerializerOptions);
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
