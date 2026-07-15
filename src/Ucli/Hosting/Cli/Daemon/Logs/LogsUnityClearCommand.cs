using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Provides the logs unity clear CLI command entry point. </summary>
internal sealed class LogsUnityClearCommand
{
    private readonly ILogsUnityClearService logsUnityClearService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the LogsUnityClearCommand class. </summary>
    /// <param name="logsUnityClearService"> The Unity Console clear orchestration service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public LogsUnityClearCommand (
        ILogsUnityClearService logsUnityClearService,
        ICommandResultWriter commandResultWriter)
    {
        this.logsUnityClearService = logsUnityClearService ?? throw new ArgumentNullException(nameof(logsUnityClearService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the logs unity clear command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="timeout"> Optional Unity Console clear timeout in milliseconds. When omitted, timeout is resolved from config defaults. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ClearSubcommand)]
    public async Task<int> ClearAsync (
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
                UcliCommandNames.LogsUnityClear,
                normalizedTimeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var executionResult = await logsUnityClearService.ExecuteAsync(
                new LogsUnityClearServiceRequest(
                    ProjectPath: projectPath,
                    TimeoutMilliseconds: normalizedTimeoutResult.TimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = CreateCommandResult(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    private static CommandResult CreateCommandResult (LogsUnityClearServiceResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (!executionResult.IsSuccess)
        {
            return CommandResultFactory.FromExecutionError(UcliCommandNames.LogsUnityClear, executionResult.Error!);
        }

        var output = executionResult.Output!;
        return CommandResult.Success(
            command: UcliCommandNames.LogsUnityClear,
            message: "Unity Console clear completed.",
            payload: new
            {
                clearStatus = "cleared",
                timeoutMilliseconds = output.TimeoutMilliseconds,
            });
    }
}
