using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

namespace MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;

/// <summary> Represents the command payload emitted by one <c>call</c> execution. </summary>
/// <param name="RequestId"> The execute request identifier. </param>
/// <param name="Project"> The resolved Unity project identity. </param>
/// <param name="OpResults"> The per-step execution results. </param>
/// <param name="Plan"> The optional plan-equivalent payload returned by <c>--withPlan</c>. </param>
/// <param name="PostReadSource"> The optional source facts used by post-read verification. </param>
internal sealed record CallExecutionOutput (
    string RequestId,
    ProjectIdentityInfo Project,
    IReadOnlyList<OperationExecutionOperationResult> OpResults,
    CallPlanOutput? Plan,
    OperationExecutionReadPostcondition? ReadPostcondition,
    OperationExecutionPostReadSource? PostReadSource = null)
{
    /// <summary> Gets runtime operation-result violations against published assurance facts. </summary>
    public IReadOnlyList<OperationExecutionContractViolation> ContractViolations { get; init; } = [];
}
