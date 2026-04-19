using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Represents the normalized result returned from one fixed operation execution workflow. </summary>
/// <param name="ProtocolVersion"> The response protocol version. </param>
/// <param name="RequestId"> The request identifier associated with this execution. </param>
/// <param name="OpResults"> The per-step execution results. </param>
/// <param name="Errors"> The machine-readable error list. </param>
/// <param name="ExitCode"> The process exit code associated with this response. </param>
internal sealed record OperationExecuteResult (
    int ProtocolVersion,
    string RequestId,
    IReadOnlyList<IpcExecuteOperationResult> OpResults,
    IReadOnlyList<IpcError> Errors,
    int ExitCode)
{
    /// <summary> Gets a value indicating whether the operation execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;
}