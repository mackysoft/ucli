namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>ping</c> IPC response payload. </summary>
/// <param name="ServerVersion"> The server plugin version string. </param>
/// <param name="Runtime"> The server runtime identifier. </param>
/// <param name="UnityVersion"> The Unity editor version. </param>
public sealed record IpcPingResponse (
    string ServerVersion,
    string Runtime,
    string UnityVersion);
