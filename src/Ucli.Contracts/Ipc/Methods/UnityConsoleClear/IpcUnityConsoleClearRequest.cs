namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>unity.console.clear</c> IPC request payload. </summary>
/// <param name="RequestedBy"> The caller label used for bounded diagnostic logging. </param>
public sealed record IpcUnityConsoleClearRequest (
    string RequestedBy);
