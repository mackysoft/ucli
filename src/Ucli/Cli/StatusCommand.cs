using ConsoleAppFramework;
using MackySoft.Ucli.Status;

namespace MackySoft.Ucli.Cli;

/// <summary> Provides the <c>status</c> CLI command entry point. </summary>
internal sealed class StatusCommand
{
    private readonly IStatusService statusService;

    /// <summary> Initializes a new instance of the <see cref="StatusCommand" /> class. </summary>
    /// <param name="statusService"> The status service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="statusService" /> is <see langword="null" />. </exception>
    public StatusCommand (IStatusService statusService)
    {
        this.statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
    }

    /// <summary> Executes the <c>status</c> command and emits the JSON result contract. </summary>
    /// <param name="projectPath">--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="timeout">Optional daemon status timeout in milliseconds. When omitted, timeout is resolved from config defaults.</param>
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

        var executionResult = await statusService.Execute(projectPath, timeout, cancellationToken).ConfigureAwait(false);
        var result = CreateCommandResult(executionResult);
        CommandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }

    /// <summary> Creates command-level JSON result from status service execution result. </summary>
    /// <param name="executionResult"> The status service execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="executionResult" /> is <see langword="null" />. </exception>
    private static CommandResult CreateCommandResult (StatusExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: UcliCommandNames.Status,
                message: "uCLI status retrieval completed.",
                payload: new
                {
                    daemonStatus = output.DaemonStatus,
                    unityVersion = output.UnityVersion,
                    serverVersion = output.ServerVersion,
                    lifecycleState = output.LifecycleState,
                    blockingReason = output.BlockingReason,
                    compileState = output.CompileState,
                    compileGeneration = output.CompileGeneration,
                    domainReloadGeneration = output.DomainReloadGeneration,
                    canAcceptExecutionRequests = output.CanAcceptExecutionRequests,
                    runtime = output.Runtime,
                    timeoutMilliseconds = output.TimeoutMilliseconds,
                });
        }

        return CommandResultFactory.FromExecutionError(UcliCommandNames.Status, executionResult.Error!);
    }
}