namespace MackySoft.Ucli.Cli;

/// <summary> Defines protocol constants shared by all CLI JSON results. </summary>
internal static class CliProtocol
{
    /// <summary> Gets the protocol version written to the <c>protocolVersion</c> field. </summary>
    public const int CurrentVersion = 1;

    /// <summary> Gets the command name used when no subcommand can be identified. </summary>
    public const string RootCommand = "root";

    /// <summary> Gets the <c>status</c> value used for successful command execution. </summary>
    public const string StatusOk = "ok";

    /// <summary> Gets the <c>status</c> value used for failed command execution. </summary>
    public const string StatusError = "error";
}