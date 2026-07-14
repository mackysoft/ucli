using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents one observed <c>build.log.entry</c> stream payload. </summary>
public sealed record BuildLogEntry
{
    /// <summary> Initializes one build log entry for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public BuildLogEntry (
        Guid RunId,
        DateTimeOffset TimestampUtc,
        string Level,
        string Message,
        string? Cursor,
        string Source)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.TimestampUtc = TimestampUtc;
        this.Level = Level;
        this.Message = Message;
        this.Cursor = Cursor;
        this.Source = Source;
    }

    public Guid RunId { get; }

    public DateTimeOffset TimestampUtc { get; }

    public string Level { get; }

    public string Message { get; }

    public string? Cursor { get; }

    public string Source { get; }
}
