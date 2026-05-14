using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;

/// <summary> Represents one normalized execute-response conversion result. </summary>
/// <param name="OpResults"> The converted per-step execution results. </param>
/// <param name="Errors"> The normalized machine-readable error list. </param>
/// <param name="PlanToken"> The optional plan token carried by the response payload. </param>
/// <param name="Project"> The project identity carried by the response payload when available. </param>
internal sealed record ExecuteResponseConversionResult (
    IReadOnlyList<OperationExecutionOperationResult> OpResults,
    IReadOnlyList<OperationExecutionError> Errors,
    string? PlanToken,
    OperationExecutionReadPostcondition? ReadPostcondition,
    ProjectIdentityInfo? Project)
{
    /// <summary> Gets a value indicating whether the converted response succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;
}
