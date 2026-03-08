namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Unity oneshot bootstrap argument payload. </summary>
/// <param name="RequestPath"> The serialized IPC request file path. </param>
/// <param name="ResponsePath"> The serialized IPC response file path. </param>
public sealed record IpcOneshotBootstrapArguments (
    string RequestPath,
    string ResponsePath)
    : IpcBatchmodeBootstrapArguments;