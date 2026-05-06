using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;

/// <summary> Represents one normalized <c>plan</c> service result. </summary>
internal sealed record PlanServiceResult
{
    private PlanServiceResult (
        PlanExecutionOutput? output,
        string message,
        IReadOnlyList<OperationExecutionError> errors,
        ApplicationOutcome outcome)
    {
        Output = output;
        Message = message;
        Errors = errors;
        Outcome = outcome;
    }

    /// <summary> Gets the output payload when available. </summary>
    public PlanExecutionOutput? Output { get; }

    /// <summary> Gets the user-facing result message. </summary>
    public string Message { get; }

    /// <summary> Gets the machine-readable error list. </summary>
    public IReadOnlyList<OperationExecutionError> Errors { get; }

    /// <summary> Gets the application outcome associated with this result. </summary>
    public ApplicationOutcome Outcome { get; }

    /// <summary> Gets a value indicating whether the service execution succeeded. </summary>
    public bool IsSuccess => Outcome == ApplicationOutcome.Success;

    /// <summary> Creates a successful service result. </summary>
    /// <param name="output"> The successful output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static PlanServiceResult Success (
        PlanExecutionOutput output,
        string message)
    {
        RequestServiceResultPolicy.ValidateSuccessMessage(message);
        return new PlanServiceResult(
            RequestServiceResultPolicy.RequireSuccessOutput(output, nameof(output)),
            message,
            RequestServiceResultPolicy.EmptyErrors,
            ApplicationOutcome.Success);
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
        RequestServiceResultPolicy.ValidateFailureMessage(message);
        return new PlanServiceResult(
            output,
            message,
            RequestServiceResultPolicy.RequireFailureErrors(errors, outcome),
            outcome);
    }
}
