using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;

/// <summary> Represents one normalized <c>plan</c> service result. </summary>
/// <param name="Output"> The output payload when available. </param>
/// <param name="Message"> The user-facing result message. </param>
/// <param name="Errors"> The machine-readable error list. </param>
/// <param name="Outcome"> The application outcome associated with this result. </param>
internal sealed record PlanServiceResult (
    PlanExecutionOutput? Output,
    string Message,
    IReadOnlyList<OperationExecutionError> Errors,
    ApplicationOutcome Outcome)
{
    /// <summary> Gets a value indicating whether the service execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary> Creates a successful service result. </summary>
    /// <param name="output"> The successful output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static PlanServiceResult Success (
        PlanExecutionOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new PlanServiceResult(output, message, [], ApplicationOutcome.Success);
    }

    /// <summary> Creates a failed service result. </summary>
    /// <param name="message"> The failure message. </param>
    /// <param name="errors"> The machine-readable failure errors. </param>
    /// <param name="outcome"> The associated application outcome. </param>
    /// <param name="output"> The available output payload. </param>
    /// <returns> The failed result. </returns>
    public static PlanServiceResult Failure (
        string message,
        IReadOnlyList<OperationExecutionError> errors,
        ApplicationOutcome outcome,
        PlanExecutionOutput? output = null)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new PlanServiceResult(output, message, errors, outcome);
    }
}
