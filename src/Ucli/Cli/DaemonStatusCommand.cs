using ConsoleAppFramework;
using MackySoft.Ucli.Daemon.Command;

namespace MackySoft.Ucli.Cli;

/// <summary> Provides the <c>daemon status</c> CLI command entry point. </summary>
internal sealed class DaemonStatusCommand
{
    private readonly IDaemonStatusCommandService daemonStatusCommandService;

    /// <summary> Initializes a new instance of the <see cref="DaemonStatusCommand" /> class. </summary>
    /// <param name="daemonStatusCommandService"> The daemon-status command service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonStatusCommandService" /> is <see langword="null" />. </exception>
    public DaemonStatusCommand (IDaemonStatusCommandService daemonStatusCommandService)
    {
        this.daemonStatusCommandService = daemonStatusCommandService ?? throw new ArgumentNullException(nameof(daemonStatusCommandService));
    }

    /// <summary> Executes the <c>daemon status</c> command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="timeout"> Optional daemon status timeout in milliseconds. When omitted, timeout is resolved from config defaults. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Status)]
    public async Task<int> Status (
        string? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var executionResult = await daemonStatusCommandService.GetStatus(projectPath, timeout, cancellationToken).ConfigureAwait(false);
        var commandResult = CreateCommandResult(executionResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    /// <summary> Creates command-level JSON result from daemon-status execution result. </summary>
    /// <param name="executionResult"> The daemon-status execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="executionResult" /> is <see langword="null" />. </exception>
    private static CommandResult CreateCommandResult (DaemonStatusExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: UcliCommandNames.DaemonStatus,
                message: "uCLI daemon status retrieval completed.",
                payload: new
                {
                    daemonStatus = output.DaemonStatus,
                    timeoutMilliseconds = output.TimeoutMilliseconds,
                    session = output.Session,
                    diagnosis = output.Diagnosis,
                });
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.DaemonStatus, executionResult.Error!);
    }
}
