using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

namespace MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;

/// <summary> Represents the command payload emitted by one <c>plan</c> execution. </summary>
/// <param name="RequestId"> The execute request identifier. </param>
/// <param name="Project"> The resolved Unity project identity. </param>
/// <param name="OpResults"> The per-step execution results. </param>
/// <param name="ContractViolations"> The runtime contract violations reported by Unity. </param>
/// <param name="ReadIndex"> The static-validation preflight read-index metadata. </param>
/// <param name="PlanToken"> The issued plan token when execution succeeded. </param>
internal sealed record PlanExecutionOutput (
    string RequestId,
    ProjectIdentityInfo Project,
    IReadOnlyList<OperationExecutionOperationResult> OpResults,
    IReadOnlyList<OperationExecutionContractViolation> ContractViolations,
    ReadIndexInfo ReadIndex,
    string? PlanToken);
