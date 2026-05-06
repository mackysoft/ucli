namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Represents one machine-readable JSON contract read error for <c>.ucli/config.json</c>. </summary>
/// <param name="Kind"> The error category. </param>
internal readonly record struct UcliConfigJsonReadError (
    UcliConfigJsonReadErrorKind Kind)
{
    /// <summary> Gets the no-error value. </summary>
    public static UcliConfigJsonReadError None => default;
}
