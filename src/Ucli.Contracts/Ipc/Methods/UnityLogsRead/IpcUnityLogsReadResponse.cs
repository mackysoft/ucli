namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>unity.logs.read</c> IPC response payload. </summary>
/// <param name="Events"> The filtered Unity log events. </param>
/// <param name="NextCursor"> The opaque cursor used for the next incremental read. </param>
public sealed record IpcUnityLogsReadResponse (
    IpcUnityLogEvent[] Events,
    string NextCursor);