using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;

/// <summary> Represents one normalized execute-response conversion result. </summary>
/// <param name="OpResults"> The converted per-step execution results. </param>
/// <param name="Errors"> The normalized machine-readable error list. </param>
/// <param name="ExitCode"> The CLI exit code associated with the converted response. </param>
/// <param name="PlanToken"> The optional plan token carried by the response payload. </param>
internal sealed record ExecuteResponseConversionResult (
    IReadOnlyList<IpcExecuteOperationResult> OpResults,
    IReadOnlyList<IpcError> Errors,
    int ExitCode,
    string? PlanToken,
    IpcExecuteReadPostcondition? ReadPostcondition)
{
    /// <summary> Gets a value indicating whether the converted response succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;
}
