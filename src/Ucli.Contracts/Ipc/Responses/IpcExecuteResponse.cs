namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents an <c>execute</c> IPC response payload. </summary>
/// <param name="OpResults"> The per-operation execution results. </param>
public sealed record IpcExecuteResponse (
    IReadOnlyList<IpcExecuteOperationResult> OpResults);