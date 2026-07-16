using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one Unity log event stored in ring buffer. </summary>
    /// <param name="Timestamp"> The event timestamp and its timezone offset. </param>
    /// <param name="Level"> The normalized event level. </param>
    /// <param name="Source"> The Unity log source. </param>
    /// <param name="Message"> The user-facing message value. </param>
    /// <param name="StackTrace"> The optional stack trace. </param>
    /// <param name="Cursor"> The opaque event cursor. </param>
    internal sealed record UnityLogEvent (
        DateTimeOffset Timestamp,
        MackySoft.Ucli.Contracts.Ipc.IpcLogLevel Level,
        MackySoft.Ucli.Contracts.Ipc.IpcUnityLogSource Source,
        string Message,
        string StackTrace,
        IpcLogCursor Cursor);
}
