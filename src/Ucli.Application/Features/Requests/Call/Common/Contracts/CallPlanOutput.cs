namespace MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;

/// <summary> Represents the optional nested plan payload emitted by one <c>call --withPlan</c> execution. </summary>
internal sealed record CallPlanOutput
{
    /// <summary> Initializes the nested plan payload emitted by one <c>call --withPlan</c> execution. </summary>
    public CallPlanOutput (
        IReadOnlyList<OperationExecutionOperationResult> opResults,
        string? planToken)
    {
        ArgumentNullException.ThrowIfNull(opResults);

        OpResults = opResults;
        PlanToken = planToken;
    }

    public IReadOnlyList<OperationExecutionOperationResult> OpResults { get; init; }

    public string? PlanToken { get; init; }

    /// <summary> Gets runtime operation-result violations against published assurance facts. </summary>
    public IReadOnlyList<OperationExecutionContractViolation> ContractViolations { get; init; } = [];
}
