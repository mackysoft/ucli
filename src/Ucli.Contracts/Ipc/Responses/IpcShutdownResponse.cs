namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>shutdown</c> IPC response payload. </summary>
/// <param name="Accepted"> Whether shutdown has been accepted by daemon. </param>
/// <param name="Message"> The optional detail message. </param>
public sealed record IpcShutdownResponse (
    bool Accepted,
    string? Message);