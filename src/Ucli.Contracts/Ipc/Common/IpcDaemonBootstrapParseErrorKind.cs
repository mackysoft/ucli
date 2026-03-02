namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines machine-readable parse error kinds for daemon bootstrap arguments. </summary>
public enum IpcDaemonBootstrapParseErrorKind
{
    /// <summary> No parse error. </summary>
    None = 0,

    /// <summary> One or more required argument pairs are missing. </summary>
    MissingRequiredArguments,

    /// <summary> One or more required argument values are empty. </summary>
    EmptyRequiredValue,
}