namespace MackySoft.Ucli.Cli;

/// <summary> Defines machine-readable error code values written to CLI JSON results. </summary>
internal static class ErrorCodes
{
    /// <summary> Gets the error code used when a command exists but is not implemented yet. </summary>
    public const string CommandNotImplemented = "COMMAND_NOT_IMPLEMENTED";

    /// <summary> Gets the error code used when command arguments are invalid. </summary>
    public const string InvalidArgument = "INVALID_ARGUMENT";

    /// <summary> Gets the error code used when required <c>.ucli</c> initialization has not been completed. </summary>
    public const string NotInitialized = "NOT_INITIALIZED";

    /// <summary> Gets the error code used when read-index bootstrap cannot be completed. </summary>
    public const string ReadIndexBootstrapFailed = "READ_INDEX_BOOTSTRAP_FAILED";

    /// <summary> Gets the error code used when read-index files are malformed. </summary>
    public const string ReadIndexFormatInvalid = "READ_INDEX_FORMAT_INVALID";

    /// <summary> Gets the error code used when command requires fresh read-index but freshness is not <c>fresh</c>. </summary>
    public const string ReadIndexFreshRequired = "READ_INDEX_FRESH_REQUIRED";

    /// <summary> Gets the error code used when command execution is canceled. </summary>
    public const string Canceled = "CANCELED";

    /// <summary> Gets the error code used when IPC execution exceeds configured timeout. </summary>
    public const string IpcTimeout = "IPC_TIMEOUT";

    /// <summary> Gets the error code used when an unexpected runtime failure occurs. </summary>
    public const string InternalError = "INTERNAL_ERROR";
}