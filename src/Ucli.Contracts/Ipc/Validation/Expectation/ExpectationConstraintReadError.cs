namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Represents one <c>expect</c> contract-read error. </summary>
/// <param name="Kind"> The machine-readable error kind. </param>
/// <param name="PropertyPath"> The related property path (for example, <c>expect.count</c>). </param>
/// <param name="UnknownPropertyName"> The unknown property name when <see cref="Kind" /> is unknown-property related; otherwise <see langword="null" />. </param>
internal readonly record struct ExpectationConstraintReadError (
    ExpectationConstraintReadErrorKind Kind,
    string PropertyPath,
    string? UnknownPropertyName = null)
{
    /// <summary> Gets an empty error value that indicates success. </summary>
    public static ExpectationConstraintReadError None => new(ExpectationConstraintReadErrorKind.None, string.Empty);
}
