using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Operations;

/// <summary> Represents the aggregated static-validation result. </summary>
/// <param name="Errors"> The collected validation errors. </param>
/// <param name="Error"> The execution error when validation could not complete. </param>
internal sealed record ValidationResult (
    IReadOnlyList<ValidationError> Errors,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether validation has no errors. </summary>
    public bool IsValid => Errors.Count == 0 && Error is null;

    /// <summary> Initializes a new instance of the <see cref="ValidationResult" /> class without execution failure. </summary>
    /// <param name="errors"> The collected validation errors. </param>
    public ValidationResult (IReadOnlyList<ValidationError> errors)
        : this(errors, null)
    {
    }

    /// <summary> Creates a successful validation result with no errors. </summary>
    /// <returns> The successful validation result. </returns>
    public static ValidationResult Success ()
    {
        return new ValidationResult(Array.Empty<ValidationError>(), null);
    }

    /// <summary> Creates one failed validation result when validation could not complete. </summary>
    /// <param name="error"> The execution error that prevented validation. </param>
    /// <returns> The failed validation result. </returns>
    public static ValidationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ValidationResult(Array.Empty<ValidationError>(), error);
    }
}