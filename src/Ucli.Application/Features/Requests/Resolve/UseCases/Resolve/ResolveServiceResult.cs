using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents the normalized result returned from one <c>resolve</c> execution workflow. </summary>
internal sealed record ResolveServiceResult (
    string RequestId,
    IReadOnlyList<IpcExecuteOperationResult> OpResults,
    IReadOnlyList<IpcError> Errors,
    ApplicationOutcome Outcome,
    ReadIndexInfo ReadIndex)
{
    /// <summary> Gets a value indicating whether resolve execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;
}
