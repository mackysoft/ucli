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
        IReadOnlyList<ApplicationFailure> errors)
    {
        Output = output;
        Message = message;
        Errors = errors;
    }

    /// <summary> Gets the output payload when available. </summary>
    public ValidateExecutionOutput? Output { get; }

    /// <summary> Gets the user-facing result message. </summary>
    public string Message { get; }

    /// <summary> Gets the machine-readable error list. </summary>
    public IReadOnlyList<ApplicationFailure> Errors { get; }

    /// <summary> Gets the application outcome associated with this result. </summary>
    public ApplicationOutcome Outcome => Errors.Count == 0
        ? ApplicationOutcome.Success
        : ApplicationFailureOutcomeResolver.Resolve(Errors);

    /// <summary> Gets a value indicating whether the service execution succeeded without static validation errors. </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary> Creates a successful service result. </summary>
    /// <param name="output"> The successful output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static ValidateServiceResult Success (
        ValidateExecutionOutput output,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(output);

        return new ValidateServiceResult(
            output,
            message,
            RequestServiceResultInvariants.EmptyErrors);
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
        var errors = RequestFailureNormalizer.FromValidationErrors(validationErrors);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var failureErrors = RequestServiceResultInvariants.RequireFailureErrors(errors);

        return new ValidateServiceResult(
            output,
            message,
            failureErrors);
    }

    /// <summary> Creates an infrastructure failure result. </summary>
    /// <param name="message"> The failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <param name="output"> The available output payload. </param>
    /// <returns> The failed result. </returns>
    public static ValidateServiceResult Failure (
        string message,
        UcliCodeValue errorCode,
        ValidateExecutionOutput? output = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var error = RequestFailureNormalizer.FromTransportFailure(errorCode, message);
        var errors = RequestServiceResultInvariants.RequireFailureErrors([error]);

        return new ValidateServiceResult(
            output,
            message,
            errors);
    }
}
