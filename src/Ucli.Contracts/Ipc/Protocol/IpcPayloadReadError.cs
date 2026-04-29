namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one IPC payload read error. </summary>
/// <param name="Kind"> The machine-readable error kind. </param>
/// <param name="Message"> The diagnostic error message. </param>
public readonly record struct IpcPayloadReadError (
    IpcPayloadReadErrorKind Kind,
    string Message)
{
    /// <summary> Gets an empty error value that indicates success. </summary>
    public static IpcPayloadReadError None => new(IpcPayloadReadErrorKind.None, string.Empty);
}
