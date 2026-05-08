using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;

/// <summary> Represents one normalized <c>call</c> service result. </summary>
internal sealed record CallServiceResult
{
    private CallServiceResult (
        CallExecutionOutput? output,
        string message,
        IReadOnlyList<ApplicationFailure> errors,
        ApplicationOutcome outcome)
    {
        Output = output;
        Message = message;
        Errors = errors;
        Outcome = outcome;
    }

    /// <summary> Gets the output payload when available. </summary>
    public CallExecutionOutput? Output { get; }

    /// <summary> Gets the user-facing result message. </summary>
    public string Message { get; }

    /// <summary> Gets the machine-readable error list. </summary>
    public IReadOnlyList<ApplicationFailure> Errors { get; }

    /// <summary> Gets the application outcome associated with this result. </summary>
    public ApplicationOutcome Outcome { get; }

    /// <summary> Gets a value indicating whether the service execution succeeded. </summary>
    public bool IsSuccess => Outcome == ApplicationOutcome.Success;

    /// <summary> Creates a successful service result. </summary>
    /// <param name="output"> The successful output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static CallServiceResult Success (
        CallExecutionOutput output,
        string message)
    {
        RequestServiceResultPolicy.ValidateSuccessMessage(message);
        return new CallServiceResult(
            RequestServiceResultPolicy.RequireSuccessOutput(output, nameof(output)),
            message,
            RequestServiceResultPolicy.EmptyErrors,
            ApplicationOutcome.Success);
    }

    /// <summary> Creates a failed service result. </summary>
    /// <param name="message"> The failure message. </param>
    /// <param name="errors"> The machine-readable failure errors. </param>
    /// <param name="output"> The available output payload. </param>
    /// <returns> The failed result. </returns>
    public static CallServiceResult Failure (
        string message,
        IReadOnlyList<ApplicationFailure> errors,
        CallExecutionOutput? output = null)
    {
        RequestServiceResultPolicy.ValidateFailureMessage(message);
        var failureErrors = RequestServiceResultPolicy.RequireFailureErrors(errors);
        return new CallServiceResult(
            output,
            message,
            failureErrors,
            RequestServiceResultPolicy.ResolveFailureOutcome(failureErrors));
    }
}
