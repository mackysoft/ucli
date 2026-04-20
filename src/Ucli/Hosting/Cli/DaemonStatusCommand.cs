using ConsoleAppFramework;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;

namespace MackySoft.Ucli.Hosting.Cli;

/// <summary> Provides the <c>daemon status</c> CLI command entry point. </summary>
internal sealed class DaemonStatusCommand
{
    private readonly IDaemonStatusService daemonStatusService;

    /// <summary> Initializes a new instance of the <see cref="DaemonStatusCommand" /> class. </summary>
    /// <param name="daemonStatusService"> The daemon-status service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonStatusService" /> is <see langword="null" />. </exception>
    public DaemonStatusCommand (IDaemonStatusService daemonStatusService)
    {
        this.daemonStatusService = daemonStatusService ?? throw new ArgumentNullException(nameof(daemonStatusService));
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

        var executionResult = await daemonStatusService.GetStatus(projectPath, timeout, cancellationToken).ConfigureAwait(false);
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
                    serverVersion = output.ServerVersion,
                    runtime = output.Runtime,
                    lifecycleState = output.LifecycleState,
                    blockingReason = output.BlockingReason,
                    compileState = output.CompileState,
                    compileGeneration = output.CompileGeneration,
                    domainReloadGeneration = output.DomainReloadGeneration,
                    canAcceptExecutionRequests = output.CanAcceptExecutionRequests,
                    timeoutMilliseconds = output.TimeoutMilliseconds,
                    session = output.Session,
                    diagnosis = output.Diagnosis,
                });
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.DaemonStatus, executionResult.Error!);
    }
}