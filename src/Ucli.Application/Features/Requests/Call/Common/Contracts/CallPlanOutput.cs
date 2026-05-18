using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

namespace MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;

/// <summary> Represents the optional nested plan payload emitted by one <c>call --withPlan</c> execution. </summary>
/// <param name="RequestId"> The execute request identifier shared with the surrounding call execution. </param>
/// <param name="Project"> The resolved Unity project identity shared with the surrounding call execution. </param>
/// <param name="OpResults"> The per-step plan execution results. </param>
/// <param name="PlanToken"> The optional plan token issued by the pre-plan pass. </param>
internal sealed record CallPlanOutput (
    string RequestId,
    ProjectIdentityInfo Project,
    IReadOnlyList<OperationExecutionOperationResult> OpResults,
    string? PlanToken)
{
    /// <summary> Gets runtime operation-result violations against published assurance facts. </summary>
    public IReadOnlyList<OperationExecutionContractViolation> ContractViolations { get; init; } = [];
}
