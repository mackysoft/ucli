using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents the aggregated static-validation result. </summary>
internal sealed record ValidationResult
{
    private ValidationResult (
        IReadOnlyList<ValidationError> errors,
        ExecutionError? error)
    {
        Errors = errors;
        Error = error;
    }

    /// <summary> Gets the collected validation errors. </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary> Gets the execution error when validation could not complete. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether validation completed without errors. </summary>
    public bool IsValid => Errors.Count == 0 && Error is null;

    /// <summary> Creates a successful validation result with no errors. </summary>
    public static ValidationResult Success ()
    {
        return new ValidationResult(Array.Empty<ValidationError>(), null);
    }

    /// <summary> Creates a completed validation result containing one or more validation errors. </summary>
    /// <param name="errors"> The non-empty validation error collection. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="errors" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="errors" /> is empty or contains a <see langword="null" /> item. </exception>
    public static ValidationResult Invalid (IReadOnlyList<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var snapshot = errors.ToArray();
        if (snapshot.Length == 0)
        {
            throw new ArgumentException("Invalid validation results must contain at least one validation error.", nameof(errors));
        }

        if (Array.Exists(snapshot, static error => error is null))
        {
            throw new ArgumentException("Validation errors must not contain null entries.", nameof(errors));
        }

        return new ValidationResult(Array.AsReadOnly(snapshot), null);
    }

    /// <summary> Creates one failed validation result when validation could not complete. </summary>
    /// <param name="error"> The execution error that prevented validation. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static ValidationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ValidationResult(Array.Empty<ValidationError>(), error);
    }
}
