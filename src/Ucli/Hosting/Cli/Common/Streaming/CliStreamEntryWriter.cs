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
    public void WriteJsonEntry (
        string eventName,
        object payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(payload);

        var entry = new CliStreamEntryEnvelope(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: command,
            StreamId: streamId,
            Sequence: checked(++sequence),
            Timestamp: timeProvider.GetUtcNow().ToString("O"),
            Event: eventName,
            Payload: payload);
        errorWriter.WriteLine(JsonSerializer.Serialize(entry, SerializerOptions));
    }

    /// <summary> Writes one human-readable text entry line. </summary>
    public void WriteTextEntry (string text)
    {
        errorWriter.WriteLine(CliTextEntrySanitizer.Sanitize(text));
    }

    private sealed record CliStreamEntryEnvelope (
        int ProtocolVersion,
        string Command,
        string StreamId,
        long Sequence,
        string Timestamp,
        string Event,
        object Payload);
}
