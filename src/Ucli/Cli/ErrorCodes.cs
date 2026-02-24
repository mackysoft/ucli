namespace MackySoft.Ucli.Cli
{
    /// <summary> Defines machine-readable error code values written to CLI JSON results. </summary>
    internal static class ErrorCodes
    {
        /// <summary> Gets the error code used when a command exists but is not implemented yet. </summary>
        public const string CommandNotImplemented = "COMMAND_NOT_IMPLEMENTED";

        /// <summary> Gets the error code used when command arguments are invalid. </summary>
        public const string InvalidArgument = "INVALID_ARGUMENT";

        /// <summary> Gets the error code used when an unexpected runtime failure occurs. </summary>
        public const string InternalError = "INTERNAL_ERROR";
    }
}