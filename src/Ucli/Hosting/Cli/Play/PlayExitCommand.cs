using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Provides the <c>play exit</c> CLI command entry point. </summary>
internal sealed class PlayExitCommand
{
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="PlayExitCommand" /> class. </summary>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public PlayExitCommand (ICommandResultWriter commandResultWriter)
    {
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>play exit</c> and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ExitSubcommand)]
    public int Exit (
        string? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var commandResult = CommandResult.NotImplemented(UcliCommandNames.PlayExit);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
