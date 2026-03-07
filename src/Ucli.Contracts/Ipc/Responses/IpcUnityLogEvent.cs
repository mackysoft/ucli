namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Unity log event returned by <c>unity.logs.read</c>. </summary>
/// <param name="Timestamp"> The event timestamp in ISO 8601 format. </param>
/// <param name="Level"> The normalized event level (<c>error|warning|info</c>). </param>
/// <param name="Source"> The Unity log source (<c>compile|runtime</c>). </param>
/// <param name="Message"> The normalized user-facing message. </param>
/// <param name="StackTrace"> The optional stack trace included by request options. </param>
/// <param name="Cursor"> The opaque cursor assigned to this event. </param>
public sealed record IpcUnityLogEvent (
    string Timestamp,
    string Level,
    string Source,
    string Message,
    string? StackTrace,
    string Cursor);