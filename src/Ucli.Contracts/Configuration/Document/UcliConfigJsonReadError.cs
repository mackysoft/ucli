namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Represents one machine-readable JSON contract read error for <c>.ucli/config.json</c>. </summary>
/// <param name="Kind"> The error category. </param>
/// <param name="PropertyName"> The related property name when available. </param>
internal readonly record struct UcliConfigJsonReadError (
    UcliConfigJsonReadErrorKind Kind,
    string? PropertyName)
{
    /// <summary> Gets the no-error value. </summary>
    public static UcliConfigJsonReadError None => default;
}
