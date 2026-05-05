using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

namespace MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;

/// <summary> Represents the command payload emitted by one <c>plan</c> execution. </summary>
/// <param name="RequestId"> The execute request identifier. </param>
/// <param name="OpResults"> The per-step execution results. </param>
/// <param name="ReadIndex"> The static-validation preflight read-index metadata. </param>
/// <param name="PlanToken"> The issued plan token when execution succeeded. </param>
internal sealed record PlanExecutionOutput (
    string RequestId,
    IReadOnlyList<OperationExecutionOperationResult> OpResults,
    ReadIndexInfo ReadIndex,
    string? PlanToken);
