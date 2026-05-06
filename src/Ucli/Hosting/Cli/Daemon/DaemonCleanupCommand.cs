using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Daemon;

/// <summary> Provides the daemon cleanup CLI command entry point. </summary>
internal sealed class DaemonCleanupCommand
{
    private readonly IDaemonCleanupService daemonCleanupService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the DaemonCleanupCommand class. </summary>
    /// <param name="daemonCleanupService"> The daemon-cleanup service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when daemonCleanupService is null. </exception>
    public DaemonCleanupCommand (
        IDaemonCleanupService daemonCleanupService,
        ICommandResultWriter? commandResultWriter = null)
    {
        this.daemonCleanupService = daemonCleanupService ?? throw new ArgumentNullException(nameof(daemonCleanupService));
        this.commandResultWriter = commandResultWriter ?? CommandResultWriter.CreateDefault();
    }

    /// <summary> Executes the daemon cleanup command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="timeout"> Optional daemon cleanup timeout in milliseconds. When omitted, timeout is resolved from config defaults. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.CleanupSubcommand)]
    public async Task<int> Cleanup (
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
                UcliCommandNames.DaemonCleanup,
                normalizedTimeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var executionResult = await daemonCleanupService.Cleanup(
                projectPath,
                normalizedTimeoutResult.TimeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = CreateCommandResult(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    /// <summary> Creates command-level JSON result from daemon-cleanup execution result. </summary>
    /// <param name="executionResult"> The daemon-cleanup execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when executionResult is null. </exception>
    private static CommandResult CreateCommandResult (DaemonCleanupExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: UcliCommandNames.DaemonCleanup,
                message: "uCLI daemon cleanup completed.",
                payload: new
                {
                    cleanupStatus = DaemonCommandOutputProjector.ToCleanupStatus(output.CleanupStatus),
                    skipReason = DaemonCommandOutputProjector.ToCleanupSkipReason(output.SkipReason),
                    timeoutMilliseconds = output.TimeoutMilliseconds,
                });
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.DaemonCleanup, executionResult.Error!);
    }
}
