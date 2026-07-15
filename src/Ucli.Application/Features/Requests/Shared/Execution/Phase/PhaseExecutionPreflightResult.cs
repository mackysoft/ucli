using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;

/// <summary> Represents the result of executing request preflight before phase execution. </summary>
internal sealed record PhaseExecutionPreflightResult
{
    private PhaseExecutionPreflightResult (
        PhaseExecutionPreparedRequest? preparedRequest,
        IReadOnlyList<ValidationError> validationErrors,
        ExecutionError? error,
        UcliCode? errorCode)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (error is null)
        {
            ArgumentNullException.ThrowIfNull(preparedRequest);
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
        ValidationErrors = validationErrors;
        Error = error;
        ErrorCode = errorCode;
    }

    public PhaseExecutionPreparedRequest? PreparedRequest { get; }

    public IReadOnlyList<ValidationError> ValidationErrors { get; }

    public ExecutionError? Error { get; }

    public UcliCode? ErrorCode { get; }

    /// <summary> Gets a value indicating whether preflight succeeded. </summary>
    public bool IsSuccess => ValidationErrors.Count == 0 && Error is null;

    /// <summary> Gets a value indicating whether preflight failed due to static validation errors. </summary>
    public bool HasValidationErrors => ValidationErrors.Count > 0;

    /// <summary> Creates a successful preflight result. </summary>
    /// <param name="preparedRequest"> The prepared request model. </param>
    /// <returns> The successful preflight result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="preparedRequest" /> is <see langword="null" />. </exception>
    public static PhaseExecutionPreflightResult Success (PhaseExecutionPreparedRequest preparedRequest)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        return new PhaseExecutionPreflightResult(
            preparedRequest,
            Array.Empty<ValidationError>(),
            error: null,
            errorCode: null);
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
            preparedRequest,
            validationErrors,
            error: null,
            errorCode: null);
    }

    /// <summary> Creates a failed preflight result with an infrastructure error. </summary>
    /// <param name="error"> The infrastructure error. </param>
    /// <param name="errorCode"> The machine-readable error code associated with <paramref name="error" />. When omitted, one code is derived from the error kind. </param>
    /// <returns> The failed preflight result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static PhaseExecutionPreflightResult Failure (
        ExecutionError error,
        PhaseExecutionPreparedRequest? preparedRequest = null,
        UcliCode? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new PhaseExecutionPreflightResult(
            preparedRequest,
            Array.Empty<ValidationError>(),
            error,
            errorCode ?? ExecutionErrorCodeMapper.ToCode(error));
    }
}
