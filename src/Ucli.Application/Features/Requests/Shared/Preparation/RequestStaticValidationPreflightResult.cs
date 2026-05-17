using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

/// <summary> Represents the result of executing shared static-validation preflight for one request. </summary>
/// <param name="PreparedRequest"> The prepared request context when available. </param>
/// <param name="ReadIndex"> The emitted read-index payload when available. </param>
/// <param name="ValidationErrors"> The static validation errors when validation failed; otherwise an empty collection. </param>
/// <param name="Error"> The infrastructure error when preflight failed; otherwise <see langword="null" />. </param>
/// <param name="ErrorCode"> The machine-readable error code associated with <paramref name="Error" />; otherwise <see langword="null" />. </param>
internal sealed record RequestStaticValidationPreflightResult (
    PreparedRequestContext? PreparedRequest,
    ReadIndexInfo? ReadIndex,
    IReadOnlyList<ValidationError> ValidationErrors,
    ExecutionError? Error,
    UcliCode? ErrorCode)
{
    /// <summary> Gets a value indicating whether static-validation preflight succeeded. </summary>
    public bool IsSuccess => PreparedRequest is not null && ReadIndex is not null && ValidationErrors.Count == 0 && Error is null && ErrorCode is null;

    /// <summary> Gets a value indicating whether static-validation preflight failed due to validation errors. </summary>
    public bool HasValidationErrors => PreparedRequest is not null && ReadIndex is not null && ValidationErrors.Count > 0 && Error is null && ErrorCode is null;

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
            PreparedRequest: preparedRequest,
            ReadIndex: readIndex,
            ValidationErrors: Array.Empty<ValidationError>(),
            Error: null,
            ErrorCode: null);
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
            PreparedRequest: preparedRequest,
            ReadIndex: readIndex,
            ValidationErrors: validationErrors,
            Error: null,
            ErrorCode: null);
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
            PreparedRequest: preparedRequest,
            ReadIndex: readIndex,
            ValidationErrors: Array.Empty<ValidationError>(),
            Error: error,
            ErrorCode: errorCode.HasValue && errorCode.Value.IsValid
                ? errorCode.Value
                : ExecutionErrorCodeMapper.ToCode(error));
    }
}
