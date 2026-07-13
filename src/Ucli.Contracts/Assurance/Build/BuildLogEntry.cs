namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents one observed <c>build.log.entry</c> stream payload. </summary>
public sealed record BuildLogEntry (
    Guid RunId,
    DateTimeOffset TimestampUtc,
    string Level,
    string Message,
    string? Cursor,
    string Source);
