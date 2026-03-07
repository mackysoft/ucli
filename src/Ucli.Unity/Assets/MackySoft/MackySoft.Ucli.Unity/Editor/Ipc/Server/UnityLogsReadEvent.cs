namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one Unity log event after read-time stack-trace shaping. </summary>
    internal sealed record UnityLogsReadEvent (
        string Timestamp,
        string Level,
        string Source,
        string Message,
        string StackTrace,
        string Cursor);
}
