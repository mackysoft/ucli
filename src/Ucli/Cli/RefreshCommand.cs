using ConsoleAppFramework;
using MackySoft.Ucli.Execution.OperationExecute;
using MackySoft.Ucli.Refresh;

namespace MackySoft.Ucli.Cli;

/// <summary> Provides the <c>refresh</c> CLI command entry point. </summary>
internal sealed class RefreshCommand
{
    private readonly IRefreshService refreshService;

    /// <summary> Initializes a new instance of the <see cref="RefreshCommand" /> class. </summary>
    /// <param name="refreshService"> The refresh service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="refreshService" /> is <see langword="null" />. </exception>
    public RefreshCommand (IRefreshService refreshService)
    {
        this.refreshService = refreshService ?? throw new ArgumentNullException(nameof(refreshService));
    }

    /// <summary> Executes the <c>refresh</c> command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (<c>auto|daemon|oneshot</c>).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet <c>ready</c>.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Refresh)]
    public async Task<int> Refresh (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();

        var executionResult = await refreshService.Execute(
                projectPath,
                mode,
                timeout,
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = CreateCommandResult(executionResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    /// <summary> Creates the command-level JSON result from one refresh execution result. </summary>
    /// <param name="executionResult"> The refresh execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="executionResult" /> is <see langword="null" />. </exception>
    private static CommandResult CreateCommandResult (OperationExecuteResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        var payload = new
        {
            requestId = executionResult.RequestId,
            opResults = executionResult.OpResults,
        };

        if (executionResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.Refresh,
                message: "uCLI refresh completed.",
                payload: payload);
        }

        var errors = new CommandError[executionResult.Errors.Count];
        for (var i = 0; i < executionResult.Errors.Count; i++)
        {
            var error = executionResult.Errors[i];
            errors[i] = new CommandError(error.Code, error.Message, error.OpId);
        }

        return new CommandResult(
            ProtocolVersion: executionResult.ProtocolVersion,
            Command: UcliCommandNames.Refresh,
            Status: MackySoft.Ucli.Contracts.Ipc.IpcProtocol.StatusError,
            ExitCode: executionResult.ExitCode,
            Message: ResolveFailureMessage(executionResult.Errors),
            Payload: payload,
            Errors: errors);
    }

    /// <summary> Resolves the refresh failure message from one machine-readable error list. </summary>
    /// <param name="errors"> The machine-readable error list. </param>
    /// <returns> The first available error message, or a fallback message when missing. </returns>
    private static string ResolveFailureMessage (IReadOnlyList<MackySoft.Ucli.Contracts.Ipc.IpcError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        for (var i = 0; i < errors.Count; i++)
        {
            var error = errors[i];
            if (!string.IsNullOrWhiteSpace(error.Message))
            {
                return error.Message;
            }
        }

        return "uCLI refresh failed.";
    }
}