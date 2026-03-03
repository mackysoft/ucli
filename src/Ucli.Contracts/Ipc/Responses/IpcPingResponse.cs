namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>ping</c> IPC response payload. </summary>
/// <param name="ServerVersion"> The server version string. </param>
/// <param name="Runtime"> The server runtime identifier. </param>
/// <param name="UnityVersion"> The Unity editor version. </param>
/// <param name="CompileState"> The compile-state value. </param>
public sealed record IpcPingResponse (
    string ServerVersion,
    string Runtime,
    string UnityVersion,
    string CompileState);