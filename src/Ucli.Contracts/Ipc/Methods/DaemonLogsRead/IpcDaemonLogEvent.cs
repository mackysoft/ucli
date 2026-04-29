namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one daemon log event returned by <c>daemon.logs.read</c>. </summary>
/// <param name="Timestamp"> The event timestamp in ISO 8601 format. </param>
/// <param name="Level"> The normalized event level (<c>error|warning|info</c>). </param>
/// <param name="Category"> The daemon log category. </param>
/// <param name="Message"> The normalized user-facing message. </param>
/// <param name="Raw"> The optional raw detail payload. </param>
/// <param name="Cursor"> The opaque cursor assigned to this event. </param>
public sealed record IpcDaemonLogEvent (
    string Timestamp,
    string Level,
    string Category,
    string Message,
    string? Raw,
    string Cursor);
