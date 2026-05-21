using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Daemon;

/// <summary> Provides the daemon status CLI command entry point. </summary>
internal sealed class DaemonStatusCommand
{
    private readonly IDaemonStatusService daemonStatusService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the DaemonStatusCommand class. </summary>
    /// <param name="daemonStatusService"> The daemon-status service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when daemonStatusService is null. </exception>
    public DaemonStatusCommand (
        IDaemonStatusService daemonStatusService,
        ICommandResultWriter commandResultWriter)
    {
        this.daemonStatusService = daemonStatusService ?? throw new ArgumentNullException(nameof(daemonStatusService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the daemon status command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="timeout"> Optional daemon status timeout in milliseconds. When omitted, timeout is resolved from config defaults. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Status)]
    public async Task<int> StatusAsync (
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
                UcliCommandNames.DaemonStatus,
                normalizedTimeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var executionResult = await daemonStatusService.GetStatusAsync(
                projectPath,
                normalizedTimeoutResult.TimeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = CreateCommandResult(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    /// <summary> Creates command-level JSON result from daemon-status execution result. </summary>
    /// <param name="executionResult"> The daemon-status execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when executionResult is null. </exception>
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
                    daemonStatus = DaemonStatusPayloadCodec.ToValue(output.DaemonStatus),
                    serverVersion = output.ServerVersion,
                    editorMode = output.EditorMode,
                    lifecycleState = output.LifecycleState,
                    blockingReason = output.BlockingReason,
                    compileState = output.CompileState,
                    compileGeneration = output.CompileGeneration,
                    domainReloadGeneration = output.DomainReloadGeneration,
                    canAcceptExecutionRequests = output.CanAcceptExecutionRequests,
                    observedAtUtc = output.ObservedAtUtc,
                    actionRequired = output.ActionRequired,
                    primaryDiagnostic = output.PrimaryDiagnostic,
                    playMode = output.PlayMode,
                    timeoutMilliseconds = output.TimeoutMilliseconds,
                    session = output.Session,
                    diagnosis = output.Diagnosis,
                    lastLaunchAttempt = output.LastLaunchAttempt,
                });
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.DaemonStatus, executionResult.Error!);
    }
}
