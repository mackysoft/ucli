namespace MackySoft.Ucli.Cli
{
    /// <summary> Defines process exit codes emitted by the CLI runtime. </summary>
    internal enum CliExitCode
    {
        /// <summary> Indicates successful command completion. </summary>
        Success = 0,

        /// <summary> Indicates that command arguments could not be parsed or validated. </summary>
        InvalidArgument = 3,

        /// <summary> Indicates that command execution failed because of a tool-level error. </summary>
        ToolError = 4,
    }
}