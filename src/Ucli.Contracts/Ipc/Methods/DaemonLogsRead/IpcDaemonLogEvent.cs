using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one daemon log event returned by <c>daemon.logs.read</c>. </summary>
public sealed record IpcDaemonLogEvent
{
    /// <summary> Initializes one daemon log event. </summary>
    [JsonConstructor]
    public IpcDaemonLogEvent (
        DateTimeOffset Timestamp,
        IpcLogLevel Level,
        string Category,
        string Message,
        string? Raw,
        string Cursor)
    {
        if (!ContractLiteralCodec.IsDefined(Level))
        {
            throw new ArgumentOutOfRangeException(nameof(Level), Level, "Log event level must identify one emitted level.");
        }

        this.Timestamp = ContractArgumentGuard.RequireTimestamp(Timestamp, nameof(Timestamp));
        this.Level = Level;
        this.Category = ContractArgumentGuard.RequireValue(Category, nameof(Category));
        this.Message = ContractArgumentGuard.RequireValue(Message, nameof(Message));
        this.Raw = Raw;
        this.Cursor = ContractArgumentGuard.RequireValue(Cursor, nameof(Cursor));
    }

    /// <summary> Gets the event timestamp and its timezone offset. </summary>
    [JsonConverter(typeof(IpcTimestampJsonConverter))]
    public DateTimeOffset Timestamp { get; }

    /// <summary> Gets the event level. </summary>
    public IpcLogLevel Level { get; }

    /// <summary> Gets the daemon log category. </summary>
    public string Category { get; }

    /// <summary> Gets the normalized user-facing message. </summary>
    public string Message { get; }

    /// <summary> Gets the optional raw detail payload. </summary>
    public string? Raw { get; }

    /// <summary> Gets the opaque cursor assigned to this event. </summary>
    public string Cursor { get; }
}
