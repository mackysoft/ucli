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

/// <summary> Provides the daemon stop CLI command entry point. </summary>
internal sealed class DaemonStopCommand
{
    private readonly IDaemonStopService daemonStopService;

    /// <summary> Initializes a new instance of the DaemonStopCommand class. </summary>
    /// <param name="daemonStopService"> The daemon-stop service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when daemonStopService is null. </exception>
    public DaemonStopCommand (IDaemonStopService daemonStopService)
    {
        this.daemonStopService = daemonStopService ?? throw new ArgumentNullException(nameof(daemonStopService));
    }

    /// <summary> Executes the daemon stop command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="timeout"> Optional daemon stop timeout in milliseconds. When omitted, timeout is resolved from config defaults. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.StopSubcommand)]
    public async Task<int> Stop (
        string? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var executionResult = await daemonStopService.Stop(projectPath, timeout, cancellationToken).ConfigureAwait(false);
        var commandResult = CreateCommandResult(executionResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    /// <summary> Creates command-level JSON result from daemon-stop execution result. </summary>
    /// <param name="executionResult"> The daemon-stop execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when executionResult is null. </exception>
    private static CommandResult CreateCommandResult (DaemonStopExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: UcliCommandNames.DaemonStop,
                message: "uCLI daemon stop completed.",
                payload: new
                {
                    stopStatus = DaemonCommandOutputProjector.ToStopStatus(output.StopStatus),
                    daemonStatus = DaemonCommandOutputProjector.ToStatus(output.DaemonStatus),
                    timeoutMilliseconds = output.TimeoutMilliseconds,
                    session = output.Session,
                });
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.DaemonStop, executionResult.Error!);
    }
}
