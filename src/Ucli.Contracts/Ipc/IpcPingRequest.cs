namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>ping</c> IPC request payload. </summary>
/// <param name="ClientVersion"> The client runtime version string. </param>
public sealed record IpcPingRequest (
    string ClientVersion);
