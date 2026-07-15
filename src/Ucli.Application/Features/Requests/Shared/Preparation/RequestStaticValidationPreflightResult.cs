using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

/// <summary> Represents the result of executing shared static-validation preflight for one request. </summary>
internal sealed record RequestStaticValidationPreflightResult
{
    private RequestStaticValidationPreflightResult (
        PreparedRequestContext? preparedRequest,
        ReadIndexInfo? readIndex,
        IReadOnlyList<ValidationError> validationErrors,
        ExecutionError? error,
        UcliCode? errorCode)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (error is null)
        {
            ArgumentNullException.ThrowIfNull(preparedRequest);
            ArgumentNullException.ThrowIfNull(readIndex);
            if (errorCode is not null)
            {
                throw new ArgumentException("Successful or validation-failure preflight must not contain an error code.", nameof(errorCode));
            }
        }
        else
        {
            ArgumentNullException.ThrowIfNull(errorCode);
            if (validationErrors.Count != 0)
            {
                throw new ArgumentException("Infrastructure-failure preflight must not contain validation errors.", nameof(validationErrors));
            }
        }

        PreparedRequest = preparedRequest;
        ReadIndex = readIndex;
        ValidationErrors = validationErrors;
        Error = error;
        ErrorCode = errorCode;
    }

    public PreparedRequestContext? PreparedRequest { get; }

    public ReadIndexInfo? ReadIndex { get; }

    public IReadOnlyList<ValidationError> ValidationErrors { get; }

    public ExecutionError? Error { get; }

    public UcliCode? ErrorCode { get; }

    /// <summary> Gets a value indicating whether static-validation preflight succeeded. </summary>
    public bool IsSuccess => ValidationErrors.Count == 0 && Error is null;

    /// <summary> Gets a value indicating whether static-validation preflight failed due to validation errors. </summary>
    public bool HasValidationErrors => ValidationErrors.Count > 0;

    /// <summary> Creates a successful static-validation preflight result. </summary>
    /// <param name="preparedRequest"> The prepared request context. </param>
    /// <param name="readIndex"> The emitted read-index payload. </param>
    /// <returns> The successful result. </returns>
    public static RequestStaticValidationPreflightResult Success (
        PreparedRequestContext preparedRequest,
        ReadIndexInfo readIndex)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(readIndex);
        return new RequestStaticValidationPreflightResult(
            preparedRequest,
            readIndex,
            Array.Empty<ValidationError>(),
            error: null,
            errorCode: null);
    }

    /// <summary> Creates a validation-failure result. </summary>
    /// <param name="preparedRequest"> The prepared request context. </param>
    /// <param name="readIndex"> The emitted read-index payload. </param>
    /// <param name="validationErrors"> The validation errors. </param>
    /// <returns> The validation-failure result. </returns>
    public static RequestStaticValidationPreflightResult ValidationFailure (
        PreparedRequestContext preparedRequest,
        ReadIndexInfo readIndex,
        IReadOnlyList<ValidationError> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (validationErrors.Count == 0)
        {
            throw new ArgumentException("Validation errors must not be empty.", nameof(validationErrors));
        }

        return new RequestStaticValidationPreflightResult(
            preparedRequest,
            readIndex,
            validationErrors,
            error: null,
            errorCode: null);
    }

    /// <summary> Creates an infrastructure failure result. </summary>
    /// <param name="error"> The infrastructure error. </param>
    /// <param name="preparedRequest"> The prepared request context when available. </param>
    /// <param name="readIndex"> The emitted read-index payload when available. </param>
    /// <param name="errorCode"> The machine-readable error code associated with <paramref name="error" />. When omitted, one code is derived from the error kind. </param>
    /// <returns> The infrastructure-failure result. </returns>
    public static RequestStaticValidationPreflightResult Failure (
        ExecutionError error,
        PreparedRequestContext? preparedRequest = null,
        ReadIndexInfo? readIndex = null,
        UcliCode? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new RequestStaticValidationPreflightResult(
            preparedRequest,
            readIndex,
            Array.Empty<ValidationError>(),
            error,
            errorCode ?? ExecutionErrorCodeMapper.ToCode(error));
    }
}
