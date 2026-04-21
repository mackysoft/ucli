using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Shared.Execution.Phase;

/// <summary> Represents the result of executing request preflight before phase execution. </summary>
/// <param name="PreparedRequest"> The prepared request model on success; otherwise <see langword="null" />. </param>
/// <param name="ValidationErrors"> The static validation errors when validation failed; otherwise an empty collection. </param>
/// <param name="Error"> The infrastructure error when preflight failed before static validation; otherwise <see langword="null" />. </param>
/// <param name="ErrorCode"> The machine-readable error code associated with <paramref name="Error" />; otherwise <see langword="null" />. </param>
internal sealed record PhaseExecutionPreflightResult (
    PhaseExecutionPreparedRequest? PreparedRequest,
    IReadOnlyList<ValidationError> ValidationErrors,
    ExecutionError? Error,
    string? ErrorCode)
{
    /// <summary> Gets a value indicating whether preflight succeeded. </summary>
    public bool IsSuccess => PreparedRequest is not null && ValidationErrors.Count == 0 && Error is null && ErrorCode is null;

    /// <summary> Gets a value indicating whether preflight failed due to static validation errors. </summary>
    public bool HasValidationErrors => ValidationErrors.Count > 0 && Error is null && ErrorCode is null;

    /// <summary> Creates a successful preflight result. </summary>
    /// <param name="preparedRequest"> The prepared request model. </param>
    /// <returns> The successful preflight result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="preparedRequest" /> is <see langword="null" />. </exception>
    public static PhaseExecutionPreflightResult Success (PhaseExecutionPreparedRequest preparedRequest)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        return new PhaseExecutionPreflightResult(
            PreparedRequest: preparedRequest,
            ValidationErrors: Array.Empty<ValidationError>(),
            Error: null,
            ErrorCode: null);
    }

    /// <summary> Creates a preflight result that failed due to static validation errors. </summary>
    /// <param name="validationErrors"> The validation errors. </param>
    /// <returns> The failed preflight result with validation errors. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="validationErrors" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="validationErrors" /> is empty. </exception>
    public static PhaseExecutionPreflightResult ValidationFailure (
        PhaseExecutionPreparedRequest preparedRequest,
        IReadOnlyList<ValidationError> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (validationErrors.Count == 0)
        {
            throw new ArgumentException("Validation errors must not be empty.", nameof(validationErrors));
        }

        return new PhaseExecutionPreflightResult(
            PreparedRequest: preparedRequest,
            ValidationErrors: validationErrors,
            Error: null,
            ErrorCode: null);
    }

    /// <summary> Creates a failed preflight result with an infrastructure error. </summary>
    /// <param name="error"> The infrastructure error. </param>
    /// <param name="errorCode"> The machine-readable error code associated with <paramref name="error" />. When omitted, one code is derived from the error kind. </param>
    /// <returns> The failed preflight result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static PhaseExecutionPreflightResult Failure (
        ExecutionError error,
        PhaseExecutionPreparedRequest? preparedRequest = null,
        string? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new PhaseExecutionPreflightResult(
            PreparedRequest: preparedRequest,
            ValidationErrors: Array.Empty<ValidationError>(),
            Error: error,
            ErrorCode: string.IsNullOrWhiteSpace(errorCode)
                ? ExecutionErrorCodeMapper.ToCode(error.Kind)
                : errorCode);
    }
}