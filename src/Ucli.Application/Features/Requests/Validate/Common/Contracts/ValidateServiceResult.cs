using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

namespace MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;

/// <summary> Represents one normalized <c>validate</c> service result. </summary>
/// <param name="Output"> The output payload when available. </param>
/// <param name="Message"> The user-facing result message. </param>
/// <param name="ErrorCode"> The machine-readable infrastructure failure code on failure; otherwise <see langword="null" />. </param>
/// <param name="ValidationErrors"> The static validation errors; empty when validation succeeded or infrastructure failed. </param>
internal sealed record ValidateServiceResult (
    ValidateExecutionOutput? Output,
    string Message,
    string? ErrorCode,
    IReadOnlyList<ValidationError> ValidationErrors)
{
    /// <summary> Gets a value indicating whether the service execution succeeded without static validation errors. </summary>
    public bool IsSuccess => ErrorCode is null && ValidationErrors.Count == 0;

    /// <summary> Gets a value indicating whether the service execution failed due to static validation errors. </summary>
    public bool HasValidationErrors => ErrorCode is null && ValidationErrors.Count > 0;

    /// <summary> Creates a successful service result. </summary>
    /// <param name="output"> The successful output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static ValidateServiceResult Success (
        ValidateExecutionOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new ValidateServiceResult(output, message, null, Array.Empty<ValidationError>());
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
        ArgumentNullException.ThrowIfNull(validationErrors);
        return new ValidateServiceResult(output, message, null, validationErrors);
    }

    /// <summary> Creates an infrastructure failure result. </summary>
    /// <param name="message"> The failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <param name="output"> The available output payload. </param>
    /// <returns> The failed result. </returns>
    public static ValidateServiceResult Failure (
        string message,
        string errorCode,
        ValidateExecutionOutput? output = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return new ValidateServiceResult(output, message, errorCode, Array.Empty<ValidationError>());
    }
}
