using System.Text;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents one observed <c>build.log.entry</c> stream payload. </summary>
public sealed record BuildLogEntry
{
    /// <summary> Initializes one valid build log entry. </summary>
    /// <exception cref="ArgumentException"> Thrown when an identifier, timestamp, or text value violates the build log contract. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Message" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Level" /> or <paramref name="Source" /> is undefined. </exception>
    [JsonConstructor]
    public BuildLogEntry (
        Guid RunId,
        DateTimeOffset TimestampUtc,
        BuildLogEntryLevel Level,
        string Message,
        IpcLogCursor? Cursor,
        BuildLogEntrySource Source)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        if (!ContractLiteralCodec.IsDefined(Level))
        {
            throw new ArgumentOutOfRangeException(nameof(Level), Level, "Build log level must be specified.");
        }

        if (!ContractLiteralCodec.IsDefined(Source))
        {
            throw new ArgumentOutOfRangeException(nameof(Source), Source, "Build log source must be specified.");
        }

        var message = ContractArgumentGuard.RequireValue(Message, nameof(Message));
        if (Encoding.UTF8.GetByteCount(message) > BuildLogEntryLimits.MaxMessageUtf8Bytes)
        {
            throw new ArgumentException("Build log message exceeds the UTF-8 byte limit.", nameof(Message));
        }

        this.RunId = RunId;
        this.TimestampUtc = ContractArgumentGuard.RequireUtcTimestamp(TimestampUtc, nameof(TimestampUtc));
        this.Level = Level;
        this.Message = message;
        this.Cursor = Cursor;
        this.Source = Source;
    }

    public Guid RunId { get; }

    public DateTimeOffset TimestampUtc { get; }

    public BuildLogEntryLevel Level { get; }

    public string Message { get; }

    public IpcLogCursor? Cursor { get; }

    public BuildLogEntrySource Source { get; }
}
