namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Defines machine-readable error kinds for JSON string-property reads. </summary>
internal enum JsonStringReadErrorKind
{
    /// <summary> No error. </summary>
    None = 0,

    /// <summary> Property is required but missing. </summary>
    Missing,

    /// <summary> Property exists but is not a JSON string. </summary>
    TypeMismatch,

    /// <summary> Property string is empty or whitespace only. </summary>
    EmptyOrWhitespace,

    /// <summary> Property string contains leading or trailing whitespace. </summary>
    OuterWhitespace,
}