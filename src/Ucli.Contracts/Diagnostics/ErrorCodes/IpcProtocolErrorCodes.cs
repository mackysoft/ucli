namespace MackySoft.Ucli.Contracts;

/// <summary> Defines IPC protocol error code values. </summary>
public static class IpcProtocolErrorCodes
{
    /// <summary> Gets the error code emitted when protocol versions are incompatible. </summary>
    public static readonly UcliCode ProtocolVersionMismatch = new("PROTOCOL_VERSION_MISMATCH");

    /// <summary> Gets the error code emitted when an IPC method is not supported. </summary>
    public static readonly UcliCode IpcMethodNotSupported = new("IPC_METHOD_NOT_SUPPORTED");

    /// <summary> Gets the error code emitted when a frame exceeds the configured upper bound. </summary>
    public static readonly UcliCode IpcFrameTooLarge = new("IPC_FRAME_TOO_LARGE");
}
