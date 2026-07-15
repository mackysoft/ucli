using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Unity log event returned by <c>unity.logs.read</c>. </summary>
public sealed record IpcUnityLogEvent
{
    /// <summary> Initializes one Unity log event. </summary>
    [JsonConstructor]
    public IpcUnityLogEvent (
        DateTimeOffset Timestamp,
        IpcLogLevel Level,
        IpcUnityLogSource Source,
        string Message,
        string? StackTrace,
        string Cursor)
    {
        if (!ContractLiteralCodec.IsDefined(Level))
        {
            throw new ArgumentOutOfRangeException(nameof(Level), Level, "Log event level must identify one emitted level.");
        }

        if (!ContractLiteralCodec.IsDefined(Source))
        {
            throw new ArgumentOutOfRangeException(nameof(Source), Source, "Unity log event source must identify one emitted source.");
        }

        this.Timestamp = ContractArgumentGuard.RequireTimestamp(Timestamp, nameof(Timestamp));
        this.Level = Level;
        this.Source = Source;
        this.Message = ContractArgumentGuard.RequireValue(Message, nameof(Message));
        this.StackTrace = StackTrace;
        this.Cursor = ContractArgumentGuard.RequireValue(Cursor, nameof(Cursor));
    }

    /// <summary> Gets the event timestamp and its timezone offset. </summary>
    [JsonConverter(typeof(IpcTimestampJsonConverter))]
    public DateTimeOffset Timestamp { get; }

    /// <summary> Gets the event level. </summary>
    public IpcLogLevel Level { get; }

    /// <summary> Gets the Unity log source. </summary>
    public IpcUnityLogSource Source { get; }

    /// <summary> Gets the normalized user-facing message. </summary>
    public string Message { get; }

    /// <summary> Gets the optional stack trace included by request options. </summary>
    public string? StackTrace { get; }

    /// <summary> Gets the opaque cursor assigned to this event. </summary>
    public string Cursor { get; }
}
