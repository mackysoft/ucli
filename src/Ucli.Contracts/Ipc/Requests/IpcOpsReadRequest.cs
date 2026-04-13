namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one <c>ops.read</c> IPC request payload. </summary>
/// <param name="FailFast"> Whether execution should fail immediately instead of waiting for lifecycle readiness. </param>
public sealed record IpcOpsReadRequest (
    bool FailFast = false);