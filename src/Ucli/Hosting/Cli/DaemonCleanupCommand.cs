using ConsoleAppFramework;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Common;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;

namespace MackySoft.Ucli.Hosting.Cli;

/// <summary> Provides the <c>daemon cleanup</c> CLI command entry point. </summary>
internal sealed class DaemonCleanupCommand
{
    private readonly IDaemonCleanupService daemonCleanupService;

    /// <summary> Initializes a new instance of the <see cref="DaemonCleanupCommand" /> class. </summary>
    /// <param name="daemonCleanupService"> The daemon-cleanup service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonCleanupService" /> is <see langword="null" />. </exception>
    public DaemonCleanupCommand (IDaemonCleanupService daemonCleanupService)
    {
        this.daemonCleanupService = daemonCleanupService ?? throw new ArgumentNullException(nameof(daemonCleanupService));
    }

    /// <summary> Executes the <c>daemon cleanup</c> command and emits the JSON result contract. </summary>
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

        var executionResult = await daemonCleanupService.Cleanup(projectPath, timeout, cancellationToken).ConfigureAwait(false);
        var commandResult = CreateCommandResult(executionResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    /// <summary> Creates command-level JSON result from daemon-cleanup execution result. </summary>
    /// <param name="executionResult"> The daemon-cleanup execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="executionResult" /> is <see langword="null" />. </exception>
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
                    cleanupStatus = output.CleanupStatus,
                    skipReason = output.SkipReason,
                    timeoutMilliseconds = output.TimeoutMilliseconds,
                });
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.DaemonCleanup, executionResult.Error!);
    }
}