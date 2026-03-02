namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Represents machine-readable JSON contract read failure categories for <c>.ucli/config.json</c>. </summary>
internal enum UcliConfigJsonReadErrorKind
{
    None = 0,
    RootTypeMismatch = 1,
    MissingProperty = 2,
    PropertyTypeMismatch = 3,
    ArrayElementTypeMismatch = 4,
    ObjectPropertyTypeMismatch = 5,
    UnknownProperty = 6,
}