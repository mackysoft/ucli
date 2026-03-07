namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one Unity log event stored in ring buffer. </summary>
    /// <param name="Sequence"> The monotonic stream sequence value. </param>
    /// <param name="Timestamp"> The event timestamp in ISO 8601 format. </param>
    /// <param name="Level"> The normalized event level. </param>
    /// <param name="Source"> The Unity log source. </param>
    /// <param name="Message"> The user-facing message value. </param>
    /// <param name="StackTrace"> The optional stack trace. </param>
    /// <param name="Cursor"> The opaque event cursor. </param>
    internal sealed record UnityLogEvent (
        long Sequence,
        string Timestamp,
        string Level,
        string Source,
        string Message,
        string StackTrace,
        string Cursor);
}
