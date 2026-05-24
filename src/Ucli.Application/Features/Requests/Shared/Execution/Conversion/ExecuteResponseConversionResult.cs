namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;

/// <summary> Represents one normalized execute-response conversion result. </summary>
/// <param name="OpResults"> The converted per-step execution results. </param>
/// <param name="Errors"> The normalized machine-readable error list. </param>
/// <param name="ContractViolations"> The runtime operation-result violations against published assurance facts. </param>
/// <param name="PlanToken"> The optional plan token carried by the response payload. </param>
/// <param name="ReadPostcondition"> The optional mutation-to-read postcondition carried by the response payload. </param>
/// <param name="PostReadSource"> The optional source facts used by post-read verification. </param>
/// <param name="Project"> The project identity carried by the response payload when available. </param>
internal sealed record ExecuteResponseConversionResult (
    IReadOnlyList<OperationExecutionOperationResult> OpResults,
    IReadOnlyList<OperationExecutionError> Errors,
    IReadOnlyList<OperationExecutionContractViolation> ContractViolations,
    string? PlanToken,
    OperationExecutionReadPostcondition? ReadPostcondition,
    OperationExecutionPostReadSource? PostReadSource,
    ProjectIdentityInfo? Project)
{
    /// <summary> Gets a value indicating whether the converted response succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;
}
