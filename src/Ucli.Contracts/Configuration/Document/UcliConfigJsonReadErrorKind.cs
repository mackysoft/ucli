namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Represents machine-readable JSON contract read failure categories for <c>.ucli/config.json</c>. </summary>
internal enum UcliConfigJsonReadErrorKind
{
    None = 0,
    RootTypeMismatch = 1,
}
