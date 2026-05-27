using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Daemon;

/// <summary> Provides the daemon start CLI command entry point. </summary>
internal sealed class DaemonStartCommand
{
    private readonly IDaemonStartService daemonStartService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the DaemonStartCommand class. </summary>
    /// <param name="daemonStartService"> The daemon-start service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when daemonStartService is null. </exception>
    public DaemonStartCommand (
        IDaemonStartService daemonStartService,
        ICommandResultWriter commandResultWriter)
    {
        this.daemonStartService = daemonStartService ?? throw new ArgumentNullException(nameof(daemonStartService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the daemon start command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="timeout"> Optional daemon start timeout in milliseconds. When omitted, timeout is resolved from config defaults. </param>
    /// <param name="editorMode">--editorMode, Optional daemon Editor mode (batchmode|gui).</param>
    /// <param name="onStartupBlocked">--onStartupBlocked, Optional process policy when startup is blocked before endpoint registration.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.StartSubcommand)]
    public async Task<int> StartAsync (
        string? projectPath = null,
        string? timeout = null,
        string? editorMode = null,
        string? onStartupBlocked = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            var errorResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.DaemonStart,
                normalizedTimeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedEditorModeResult = DaemonEditorModeOptionNormalizer.Normalize(editorMode);
        if (!normalizedEditorModeResult.IsSuccess)
        {
            var errorResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.DaemonStart,
                normalizedEditorModeResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedOnStartupBlockedResult = DaemonStartupBlockedProcessPolicyOptionNormalizer.Normalize(onStartupBlocked);
        if (!normalizedOnStartupBlockedResult.IsSuccess)
        {
            var errorResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.DaemonStart,
                normalizedOnStartupBlockedResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var executionResult = await daemonStartService.StartAsync(
                projectPath,
                normalizedTimeoutResult.TimeoutMilliseconds,
                normalizedEditorModeResult.EditorMode,
                normalizedOnStartupBlockedResult.Policy,
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = CreateCommandResult(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
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
                    startStatus = DaemonCommandOutputProjector.ToStartStatus(output.StartStatus),
                    daemonStatus = ContractLiteralCodec.ToValue(output.DaemonStatus),
                    lifecycleState = output.LifecycleState,
                    blockingReason = output.BlockingReason,
                    canAcceptExecutionRequests = output.CanAcceptExecutionRequests,
                    timeoutMilliseconds = output.TimeoutMilliseconds,
                    session = output.Session,
                });
        }

        if (executionResult.FailureOutput is null)
        {
            return CommandResultFactory.FromExecutionError(UcliCommandNames.DaemonStart, executionResult.Error!);
        }

        var failureOutput = executionResult.FailureOutput;
        return CommandFailureProjector.Create(
            UcliCommandNames.DaemonStart,
            ApplicationFailure.FromExecutionError(executionResult.Error!),
            payload: new
            {
                startStatus = ContractLiteralCodec.ToValue(DaemonStartStatus.Failed),
                daemonStatus = ContractLiteralCodec.ToValue(failureOutput.DaemonStatus),
                timeoutMilliseconds = failureOutput.TimeoutMilliseconds,
                session = (object?)null,
                startup = failureOutput.Startup,
                diagnosis = failureOutput.Diagnosis,
                retryDisposition = failureOutput.RetryDisposition,
                safeToRetryImmediately = failureOutput.SafeToRetryImmediately,
            });
    }
}
