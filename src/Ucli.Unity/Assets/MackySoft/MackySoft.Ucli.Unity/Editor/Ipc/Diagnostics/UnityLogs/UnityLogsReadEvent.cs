using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one Unity log event after read-time stack-trace shaping. </summary>
    internal sealed record UnityLogsReadEvent (
        DateTimeOffset Timestamp,
        MackySoft.Ucli.Contracts.Ipc.IpcLogLevel Level,
        MackySoft.Ucli.Contracts.Ipc.IpcUnityLogSource Source,
        string Message,
        string StackTrace,
        IpcLogCursor Cursor);
}
