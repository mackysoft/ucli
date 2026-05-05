using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Represents the normalized result returned from one typed-query execution workflow. </summary>
internal sealed record QueryServiceResult (
    string CommandName,
    string RequestId,
    IReadOnlyList<OperationExecutionOperationResult> OpResults,
    IReadOnlyList<OperationExecutionError> Errors,
    ApplicationOutcome Outcome,
    string Message,
    ReadIndexInfo ReadIndex)
{
    /// <summary> Gets a value indicating whether query execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;
}
