namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Unity oneshot bootstrap argument payload. </summary>
/// <param name="OutputPath"> The output file path that receives one serialized ops snapshot. </param>
public sealed record IpcOneshotBootstrapArguments (
    string OutputPath)
    : IpcBatchmodeBootstrapArguments;