using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Represents the normalized result returned from one fixed operation execution workflow. </summary>
/// <param name="RequestId"> The request identifier associated with this execution. </param>
/// <param name="OpResults"> The per-step execution results. </param>
/// <param name="Errors"> The machine-readable error list. </param>
/// <param name="Outcome"> The application outcome associated with this response. </param>
internal sealed record OperationExecuteResult (
    string RequestId,
    IReadOnlyList<OperationExecutionOperationResult> OpResults,
    IReadOnlyList<OperationExecutionError> Errors,
    ApplicationOutcome Outcome,
    OperationExecutionReadPostcondition? ReadPostcondition)
{
    /// <summary> Gets a value indicating whether the operation execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;
}
