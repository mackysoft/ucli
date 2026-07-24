using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Daemon;

/// <summary> Provides the daemon list CLI command entry point. </summary>
internal sealed class DaemonListCommand
{
    private readonly IDaemonListService daemonListService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the DaemonListCommand class. </summary>
    /// <param name="daemonListService"> The daemon-list service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when daemonListService is null. </exception>
    public DaemonListCommand (
        IDaemonListService daemonListService,
        ICommandResultWriter commandResultWriter)
    {
        this.daemonListService = daemonListService ?? throw new ArgumentNullException(nameof(daemonListService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the daemon list command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="timeout"> Optional daemon-list timeout budget in milliseconds. When omitted, timeout is resolved from config defaults. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ListSubcommand)]
    public async Task<int> ListAsync (
        string? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            var errorResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.DaemonList,
                normalizedTimeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var executionResult = await daemonListService.GetListAsync(
                projectPath,
                normalizedTimeoutResult.TimeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = CreateCommandResult(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    /// <summary> Creates command-level JSON result from daemon-list execution result. </summary>
    /// <param name="executionResult"> The daemon-list execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when executionResult is null. </exception>
    private static CommandResult CreateCommandResult (DaemonListExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: UcliCommandNames.DaemonList,
                message: output.IsComplete
                    ? "uCLI daemon list retrieval completed."
                    : "uCLI daemon list retrieval completed with partial results.",
                payload: new
                {
                    timeoutMilliseconds = output.TimeoutMilliseconds,
                    projectRelativePath = output.ProjectRelativePath,
                    isComplete = output.IsComplete,
                    completionReason = output.CompletionReason.HasValue
                        ? TextVocabulary.GetText(output.CompletionReason.Value)
                        : null,
                    remainingWorktreeCount = output.RemainingWorktreeCount,
                    items = output.Items.Select(DaemonCommandOutputProjector.ToListItem).ToArray(),
                });
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.DaemonList, executionResult.Error!);
    }
}
