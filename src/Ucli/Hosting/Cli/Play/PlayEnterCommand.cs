using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Provides the <c>play enter</c> CLI command entry point. </summary>
internal sealed class PlayEnterCommand
{
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="PlayEnterCommand" /> class. </summary>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public PlayEnterCommand (ICommandResultWriter commandResultWriter)
    {
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>play enter</c> and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.EnterSubcommand)]
    public int Enter (
        string? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var commandResult = CommandResult.NotImplemented(UcliCommandNames.PlayEnter);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
