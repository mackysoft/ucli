using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Daemon;

/// <summary> Provides the daemon start CLI command entry point. </summary>
internal sealed class DaemonStartCommand
{
    private readonly IDaemonStartService daemonStartService;

    /// <summary> Initializes a new instance of the DaemonStartCommand class. </summary>
    /// <param name="daemonStartService"> The daemon-start service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when daemonStartService is null. </exception>
    public DaemonStartCommand (IDaemonStartService daemonStartService)
    {
        this.daemonStartService = daemonStartService ?? throw new ArgumentNullException(nameof(daemonStartService));
    }

    /// <summary> Executes the daemon start command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="timeout"> Optional daemon start timeout in milliseconds. When omitted, timeout is resolved from config defaults. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.StartSubcommand)]
    public async Task<int> Start (
        string? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var executionResult = await daemonStartService.Start(projectPath, timeout, cancellationToken).ConfigureAwait(false);
        var commandResult = CreateCommandResult(executionResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    /// <summary> Creates command-level JSON result from daemon-start execution result. </summary>
    /// <param name="executionResult"> The daemon-start execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when executionResult is null. </exception>
    private static CommandResult CreateCommandResult (DaemonStartExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: UcliCommandNames.DaemonStart,
                message: "uCLI daemon start completed.",
                payload: new
                {
                    startStatus = output.StartStatus,
                    daemonStatus = output.DaemonStatus,
                    timeoutMilliseconds = output.TimeoutMilliseconds,
                    session = output.Session,
                });
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.DaemonStart, executionResult.Error!);
    }
}
