namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>play.status</c> IPC response payload. </summary>
/// <param name="Snapshot"> The lifecycle snapshot observed by the daemon. </param>
public sealed record IpcPlayStatusResponse (
    IpcPlayLifecycleSnapshot Snapshot);
