namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines machine-readable error code values shared by IPC requests and responses. </summary>
public static class IpcErrorCodes
{
    /// <summary> Gets the error code emitted when request arguments are invalid. </summary>
    public const string InvalidArgument = "INVALID_ARGUMENT";

    /// <summary> Gets the error code emitted when protocol versions are incompatible. </summary>
    public const string ProtocolVersionMismatch = "PROTOCOL_VERSION_MISMATCH";

    /// <summary> Gets the error code emitted when a request omits <c>sessionToken</c>. </summary>
    public const string SessionTokenRequired = "SESSION_TOKEN_REQUIRED";

    /// <summary> Gets the error code emitted when an IPC method is not supported. </summary>
    public const string IpcMethodNotSupported = "IPC_METHOD_NOT_SUPPORTED";

    /// <summary> Gets the error code emitted when a frame exceeds the configured upper bound. </summary>
    public const string IpcFrameTooLarge = "IPC_FRAME_TOO_LARGE";

    /// <summary> Gets the error code emitted when command execution is not yet implemented. </summary>
    public const string CommandNotImplemented = "COMMAND_NOT_IMPLEMENTED";

    /// <summary> Gets the error code emitted when an unexpected internal failure occurs. </summary>
    public const string InternalError = "INTERNAL_ERROR";
}