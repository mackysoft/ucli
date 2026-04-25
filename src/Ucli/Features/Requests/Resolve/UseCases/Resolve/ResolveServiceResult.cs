using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Execution.ReadIndex;

namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents the normalized result returned from one <c>resolve</c> execution workflow. </summary>
internal sealed record ResolveServiceResult (
    int ProtocolVersion,
    string RequestId,
    IReadOnlyList<IpcExecuteOperationResult> OpResults,
    IReadOnlyList<IpcError> Errors,
    int ExitCode,
    ReadIndexInfo ReadIndex)
{
    /// <summary> Gets a value indicating whether resolve execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;
}