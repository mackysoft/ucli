namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>daemon.logs.read</c> IPC response payload. </summary>
/// <param name="Events"> The filtered daemon log events. </param>
/// <param name="NextCursor"> The opaque cursor used for the next incremental read. </param>
public sealed record IpcDaemonLogsReadResponse (
    IpcDaemonLogEvent[] Events,
    string NextCursor);