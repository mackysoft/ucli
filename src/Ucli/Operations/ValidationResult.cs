namespace MackySoft.Ucli.Operations;

/// <summary> Represents the aggregated static-validation result. </summary>
/// <param name="Errors"> The collected validation errors. </param>
internal sealed record ValidationResult (
    IReadOnlyList<ValidationError> Errors)
{
    /// <summary> Gets a value indicating whether validation has no errors. </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary> Creates a successful validation result with no errors. </summary>
    /// <returns> The successful validation result. </returns>
    public static ValidationResult Success ()
    {
        return new ValidationResult(Array.Empty<ValidationError>());
    }
}