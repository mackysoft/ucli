using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Provides the <c>play wait</c> CLI command entry point. </summary>
internal sealed class PlayWaitCommand
{
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="PlayWaitCommand" /> class. </summary>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public PlayWaitCommand (ICommandResultWriter commandResultWriter)
    {
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>play wait</c> and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="until">Wait target (entered|exited|ready).</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.WaitSubcommand)]
    public int Wait (
        string? projectPath = null,
        string? timeout = null,
        string? until = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var commandResult = CommandResult.NotImplemented(UcliCommandNames.PlayWait);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
