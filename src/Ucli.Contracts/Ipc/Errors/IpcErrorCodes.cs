namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines machine-readable error code values shared by IPC requests and responses. </summary>
public static class IpcErrorCodes
{
    /// <summary> Gets the error code emitted when request arguments are invalid. </summary>
    public const string InvalidArgument = "INVALID_ARGUMENT";

    /// <summary> Gets the error code emitted when required workspace initialization has not been completed. </summary>
    public const string NotInitialized = "NOT_INITIALIZED";

    /// <summary> Gets the error code emitted when protocol versions are incompatible. </summary>
    public const string ProtocolVersionMismatch = "PROTOCOL_VERSION_MISMATCH";

    /// <summary> Gets the error code emitted when a request omits <c>sessionToken</c>. </summary>
    public const string SessionTokenRequired = "SESSION_TOKEN_REQUIRED";

    /// <summary> Gets the error code emitted when a request contains an invalid <c>sessionToken</c>. </summary>
    public const string SessionTokenInvalid = "SESSION_TOKEN_INVALID";

    /// <summary> Gets the error code emitted when an IPC method is not supported. </summary>
    public const string IpcMethodNotSupported = "IPC_METHOD_NOT_SUPPORTED";

    /// <summary> Gets the error code emitted when a frame exceeds the configured upper bound. </summary>
    public const string IpcFrameTooLarge = "IPC_FRAME_TOO_LARGE";

    /// <summary> Gets the error code emitted when command execution is not yet implemented. </summary>
    public const string CommandNotImplemented = "COMMAND_NOT_IMPLEMENTED";

    /// <summary> Gets the error code emitted when read-index bootstrap cannot be completed. </summary>
    public const string ReadIndexBootstrapFailed = "READ_INDEX_BOOTSTRAP_FAILED";

    /// <summary> Gets the error code emitted when read-index files are malformed. </summary>
    public const string ReadIndexFormatInvalid = "READ_INDEX_FORMAT_INVALID";

    /// <summary> Gets the error code emitted when a request requires fresh read-index but freshness is not <c>fresh</c>. </summary>
    public const string ReadIndexFreshRequired = "READ_INDEX_FRESH_REQUIRED";

    /// <summary> Gets the error code emitted when <c>call</c> requires a plan token but none is provided. </summary>
    public const string PlanTokenRequired = "PLAN_TOKEN_REQUIRED";

    /// <summary> Gets the error code emitted when a provided plan token fails structural or signature validation. </summary>
    public const string PlanTokenInvalid = "PLAN_TOKEN_INVALID";

    /// <summary> Gets the error code emitted when a provided plan token has expired. </summary>
    public const string PlanTokenExpired = "PLAN_TOKEN_EXPIRED";

    /// <summary> Gets the error code emitted when token request digest does not match the current request. </summary>
    public const string PlanTokenRequestMismatch = "PLAN_TOKEN_REQUEST_MISMATCH";

    /// <summary> Gets the error code emitted when project state changed since token issuance. </summary>
    public const string StateChangedSincePlan = "STATE_CHANGED_SINCE_PLAN";

    /// <summary> Gets the error code emitted when one request-id is reused with different request content. </summary>
    public const string RequestIdConflict = "REQUEST_ID_CONFLICT";

    /// <summary> Gets the error code emitted when Unity editor startup is still in progress. </summary>
    public const string EditorStarting = "EDITOR_STARTING";

    /// <summary> Gets the error code emitted when Unity editor is busy with internal work. </summary>
    public const string EditorBusy = "EDITOR_BUSY";

    /// <summary> Gets the error code emitted when Unity editor is compiling scripts. </summary>
    public const string EditorCompiling = "EDITOR_COMPILING";

    /// <summary> Gets the error code emitted when Unity editor is reloading the AppDomain. </summary>
    public const string EditorDomainReloading = "EDITOR_DOMAIN_RELOADING";

    /// <summary> Gets the error code emitted when Unity editor is in Play Mode. </summary>
    public const string EditorPlaymode = "EDITOR_PLAYMODE";

    /// <summary> Gets the error code emitted when a modal dialog blocks Unity editor execution. </summary>
    public const string EditorModalBlocked = "EDITOR_MODAL_BLOCKED";

    /// <summary> Gets the error code emitted when Unity editor is in Safe Mode. </summary>
    public const string EditorSafeMode = "EDITOR_SAFE_MODE";

    /// <summary> Gets the error code emitted when Unity editor shutdown is in progress. </summary>
    public const string EditorShuttingDown = "EDITOR_SHUTTING_DOWN";

    /// <summary> Gets the error code emitted when an unexpected internal failure occurs. </summary>
    public const string InternalError = "INTERNAL_ERROR";
}