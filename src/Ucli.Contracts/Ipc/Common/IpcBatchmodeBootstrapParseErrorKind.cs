namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines machine-readable parse error kinds for Unity batchmode bootstrap arguments. </summary>
public enum IpcBatchmodeBootstrapParseErrorKind
{
    /// <summary> No parse error. </summary>
    None = 0,

    /// <summary> The bootstrap target argument is missing. </summary>
    MissingTarget,

    /// <summary> The bootstrap target argument value is invalid. </summary>
    InvalidTarget,

    /// <summary> One or more required argument pairs are missing. </summary>
    MissingRequiredArguments,

    /// <summary> One or more required argument values are empty. </summary>
    EmptyRequiredValue,
}