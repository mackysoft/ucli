namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Unity batchmode bootstrap argument parse error. </summary>
/// <param name="Kind"> The machine-readable parse error kind. </param>
/// <param name="Message"> The diagnostic parse error message. </param>
public readonly record struct IpcBatchmodeBootstrapParseError (
    IpcBatchmodeBootstrapParseErrorKind Kind,
    string Message)
{
    /// <summary> Gets an empty error value that indicates success. </summary>
    public static IpcBatchmodeBootstrapParseError None => new(
        IpcBatchmodeBootstrapParseErrorKind.None,
        string.Empty);
}