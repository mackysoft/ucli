namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents Unity GUI bootstrap argument parse failure details. </summary>
/// <param name="Kind"> The parse error kind. </param>
/// <param name="Message"> The human-readable parse error message. </param>
public readonly record struct IpcGuiBootstrapParseError (
    IpcGuiBootstrapParseErrorKind Kind,
    string Message)
{
    /// <summary> Gets the no-error value. </summary>
    public static IpcGuiBootstrapParseError None => new(IpcGuiBootstrapParseErrorKind.None, string.Empty);
}
