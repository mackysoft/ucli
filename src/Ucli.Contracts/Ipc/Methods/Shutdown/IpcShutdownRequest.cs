namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>shutdown</c> IPC request payload. </summary>
/// <param name="RequestedBy"> The caller identifier for audit logging. </param>
public sealed record IpcShutdownRequest (
    string RequestedBy);