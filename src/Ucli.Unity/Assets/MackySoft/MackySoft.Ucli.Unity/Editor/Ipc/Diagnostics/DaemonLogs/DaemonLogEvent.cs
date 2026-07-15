using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one daemon control-log event stored in ring buffer. </summary>
    /// <param name="Timestamp"> The event timestamp and its timezone offset. </param>
    /// <param name="Level"> The normalized event level. </param>
    /// <param name="Category"> The daemon log category. </param>
    /// <param name="Message"> The user-facing message value. </param>
    /// <param name="Raw"> The optional raw detail payload. </param>
    /// <param name="Cursor"> The opaque event cursor. </param>
    internal sealed record DaemonLogEvent (
        DateTimeOffset Timestamp,
        MackySoft.Ucli.Contracts.Ipc.IpcLogLevel Level,
        string Category,
        string Message,
        string Raw,
        IpcLogCursor Cursor);
}
