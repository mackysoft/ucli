using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents the normalized result returned from one <c>resolve</c> execution workflow. </summary>
internal sealed record ResolveServiceResult (
    string RequestId,
    IReadOnlyList<OperationExecutionOperationResult> OpResults,
    IReadOnlyList<OperationExecutionError> Errors,
    ApplicationOutcome Outcome,
    ReadIndexInfo ReadIndex)
{
    /// <summary> Gets a value indicating whether resolve execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;
}
