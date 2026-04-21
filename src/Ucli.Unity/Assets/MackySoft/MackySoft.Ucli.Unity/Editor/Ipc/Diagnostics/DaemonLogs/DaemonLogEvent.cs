namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one daemon control-log event stored in ring buffer. </summary>
    /// <param name="Sequence"> The monotonic stream sequence value. </param>
    /// <param name="Timestamp"> The event timestamp in ISO 8601 format. </param>
    /// <param name="Level"> The normalized event level. </param>
    /// <param name="Category"> The daemon log category. </param>
    /// <param name="Message"> The user-facing message value. </param>
    /// <param name="Raw"> The optional raw detail payload. </param>
    /// <param name="Cursor"> The opaque event cursor. </param>
    internal sealed record DaemonLogEvent (
        long Sequence,
        string Timestamp,
        string Level,
        string Category,
        string Message,
        string Raw,
        string Cursor);
}