using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;

/// <summary> Represents one normalized <c>validate</c> service result. </summary>
internal sealed record ValidateServiceResult
{
    private ValidateServiceResult (
        ValidateExecutionOutput? output,
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
    public ValidateExecutionOutput? Output { get; }

    /// <summary> Gets the user-facing result message. </summary>
    public string Message { get; }

    /// <summary> Gets the machine-readable error list. </summary>
    public IReadOnlyList<ApplicationFailure> Errors { get; }

    /// <summary> Gets the application outcome associated with this result. </summary>
    public ApplicationOutcome Outcome { get; }

    /// <summary> Gets a value indicating whether the service execution succeeded without static validation errors. </summary>
    public bool IsSuccess => Outcome == ApplicationOutcome.Success;

    /// <summary> Creates a successful service result. </summary>
    /// <param name="output"> The successful output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static ValidateServiceResult Success (
        ValidateExecutionOutput output,
        string message)
    {
        RequestServiceResultPolicy.ValidateSuccessMessage(message);
        return new ValidateServiceResult(
            RequestServiceResultPolicy.RequireSuccessOutput(output, nameof(output)),
            message,
            RequestServiceResultPolicy.EmptyErrors,
            ApplicationOutcome.Success);
    }

    /// <summary> Creates a service result that failed due to static validation errors. </summary>
    /// <param name="output"> The available output payload. </param>
    /// <param name="message"> The validation-failure message. </param>
    /// <param name="validationErrors"> The validation errors. </param>
    /// <returns> The validation-failure result. </returns>
    public static ValidateServiceResult ValidationFailure (
        ValidateExecutionOutput? output,
        string message,
        IReadOnlyList<ValidationError> validationErrors)
    {
        var errors = RequestServiceResultPolicy.FromValidationErrors(validationErrors);
        RequestServiceResultPolicy.ValidateFailureMessage(message);
        var failureErrors = RequestServiceResultPolicy.RequireFailureErrors(errors);
        return new ValidateServiceResult(
            output,
            message,
            failureErrors,
            RequestServiceResultPolicy.ResolveFailureOutcome(failureErrors));
    }

    /// <summary> Creates an infrastructure failure result. </summary>
    /// <param name="message"> The failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <param name="output"> The available output payload. </param>
    /// <returns> The failed result. </returns>
    public static ValidateServiceResult Failure (
        string message,
        UcliErrorCode errorCode,
        ValidateExecutionOutput? output = null)
    {
        RequestServiceResultPolicy.ValidateFailureMessage(message);
        var error = RequestServiceResultPolicy.FromTransportFailure(errorCode, message);
        var errors = RequestServiceResultPolicy.RequireFailureErrors([error]);
        return new ValidateServiceResult(
            output,
            message,
            errors,
            RequestServiceResultPolicy.ResolveFailureOutcome(errors));
    }
}
