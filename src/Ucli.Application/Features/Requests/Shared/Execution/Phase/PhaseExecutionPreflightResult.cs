using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;

/// <summary> Represents the result of executing request preflight before phase execution. </summary>
internal sealed record PhaseExecutionPreflightResult
{
    private PhaseExecutionPreflightResult (
        PhaseExecutionPreparedRequest preparedRequest,
        IReadOnlyList<ValidationError> validationErrors,
        ApplicationFailure? error)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (error is not null)
        {
            if (validationErrors.Count != 0)
            {
                throw new ArgumentException("Failed preflight must not contain validation errors.", nameof(validationErrors));
            }
        }

        PreparedRequest = preparedRequest;
        ValidationErrors = validationErrors;
        Error = error;
    }

    public PhaseExecutionPreparedRequest PreparedRequest { get; }

    public IReadOnlyList<ValidationError> ValidationErrors { get; }

    public ApplicationFailure? Error { get; }

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
            error: null);
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
            error: null);
    }

    /// <summary> Creates a failed preflight result with a classified application failure. </summary>
    /// <param name="error"> The classified application failure. </param>
    /// <param name="preparedRequest"> The prepared request state retained for failure output. </param>
    /// <returns> The failed preflight result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static PhaseExecutionPreflightResult Failure (
        ApplicationFailure error,
        PhaseExecutionPreparedRequest preparedRequest)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(preparedRequest);
        return new PhaseExecutionPreflightResult(
            preparedRequest,
            Array.Empty<ValidationError>(),
            error);
    }
}
