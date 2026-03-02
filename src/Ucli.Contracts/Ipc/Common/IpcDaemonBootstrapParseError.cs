namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one daemon bootstrap argument parse error. </summary>
/// <param name="Kind"> The machine-readable parse error kind. </param>
/// <param name="Message"> The diagnostic parse error message. </param>
public readonly record struct IpcDaemonBootstrapParseError (
    IpcDaemonBootstrapParseErrorKind Kind,
    string Message)
{
    /// <summary> Gets an empty error value that indicates success. </summary>
    public static IpcDaemonBootstrapParseError None => new(
        IpcDaemonBootstrapParseErrorKind.None,
        string.Empty);
}