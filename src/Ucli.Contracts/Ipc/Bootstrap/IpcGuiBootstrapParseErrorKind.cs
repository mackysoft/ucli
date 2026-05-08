namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Unity GUI bootstrap argument parse error kinds. </summary>
public enum IpcGuiBootstrapParseErrorKind
{
    /// <summary> No parse error occurred. </summary>
    None,

    /// <summary> The GUI bootstrap target argument is missing. </summary>
    MissingTarget,

    /// <summary> The GUI bootstrap target value is unsupported. </summary>
    InvalidTarget,

    /// <summary> One or more required GUI bootstrap arguments are missing. </summary>
    MissingRequiredArguments,

    /// <summary> One or more required GUI bootstrap values are empty or invalid. </summary>
    InvalidRequiredValue,
}
