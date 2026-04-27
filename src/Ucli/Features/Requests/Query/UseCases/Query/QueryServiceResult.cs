using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Execution.ReadIndex;

namespace MackySoft.Ucli.Features.Requests.Query.UseCases.Query;

/// <summary> Represents the normalized result returned from one typed-query execution workflow. </summary>
internal sealed record QueryServiceResult (
    int ProtocolVersion,
    string CommandName,
    string RequestId,
    IReadOnlyList<IpcExecuteOperationResult> OpResults,
    IReadOnlyList<IpcError> Errors,
    int ExitCode,
    string Message,
    ReadIndexInfo ReadIndex)
{
    /// <summary> Gets a value indicating whether query execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;
}
