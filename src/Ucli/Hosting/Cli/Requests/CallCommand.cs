using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call.Projection;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;
using MackySoft.Ucli.Hosting.Cli.Requests.Call.Preflight;
using MackySoft.Ucli.Hosting.Cli.Requests.Input;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the call CLI command entry point. </summary>
internal sealed class CallCommand
{
    private readonly ICallService callService;

    private readonly ICallCommandPreflightService callCommandPreflightService;

    private readonly IRequestInputReader requestInputReader;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the CallCommand class. </summary>
    /// <param name="callService"> The call workflow service dependency. </param>
    /// <param name="callCommandPreflightService"> The command preflight dependency used to preserve the call payload on option failures. </param>
    /// <param name="requestInputReader"> The CLI request-input reader dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public CallCommand (
        ICallService callService,
        ICallCommandPreflightService callCommandPreflightService,
        IRequestInputReader requestInputReader,
        ICommandResultWriter commandResultWriter)
    {
        this.callService = callService ?? throw new ArgumentNullException(nameof(callService));
        this.callCommandPreflightService = callCommandPreflightService ?? throw new ArgumentNullException(nameof(callCommandPreflightService));
        this.requestInputReader = requestInputReader ?? throw new ArgumentNullException(nameof(requestInputReader));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the call command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="planToken">--planToken, Optional plan token issued by a prior plan execution.</param>
    /// <param name="withPlan">--withPlan, Includes one plan-equivalent payload alongside call output.</param>
    /// <param name="allowDangerous">--allowDangerous, Explicitly allows dangerous operations when the project config also permits them.</param>
    /// <param name="allowPlayMode">--allowPlayMode, Allows Play Mode mutation when the target is a GUI Editor session in Play Mode.</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet ready.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Call)]
    public async Task<int> CallAsync (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? planToken = null,
        bool withPlan = false,
        bool allowDangerous = false,
        bool allowPlayMode = false,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        var requestId = Guid.NewGuid();

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            var requestInputReadResult = await requestInputReader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (!requestInputReadResult.IsSuccess)
            {
                return WriteRequestReadFailure(requestInputReadResult);
            }

            var preflightResult = await callCommandPreflightService.PrepareAsync(
                    requestId,
                    projectPath,
                    requestInputReadResult.Json!,
                    cancellationToken)
                .ConfigureAwait(false);
            var failureResult = preflightResult.ToFailureResult(normalizedTimeoutResult.Error!);
            var commandFailureResult = CallCommandResultFactory.Create(failureResult);
            commandResultWriter.WriteToStandardOutput(commandFailureResult);
            return commandFailureResult.ExitCode;
        }

        var normalizedModeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!normalizedModeResult.IsSuccess)
        {
            var requestInputReadResult = await requestInputReader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (!requestInputReadResult.IsSuccess)
            {
                return WriteRequestReadFailure(requestInputReadResult);
            }

            var preflightResult = await callCommandPreflightService.PrepareAsync(
                    requestId,
                    projectPath,
                    requestInputReadResult.Json!,
                    cancellationToken)
                .ConfigureAwait(false);
            var failureResult = preflightResult.ToFailureResult(normalizedModeResult.Error!);
            var commandFailureResult = CallCommandResultFactory.Create(failureResult);
            commandResultWriter.WriteToStandardOutput(commandFailureResult);
            return commandFailureResult.ExitCode;
        }

        var serviceRequestInputReadResult = await requestInputReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (!serviceRequestInputReadResult.IsSuccess)
        {
            return WriteRequestReadFailure(serviceRequestInputReadResult);
        }

        var serviceResult = await callService.ExecuteAsync(
                requestId,
                new CallCommandInput(
                    ProjectPath: projectPath,
                    Mode: normalizedModeResult.Mode,
                    TimeoutMilliseconds: normalizedTimeoutResult.TimeoutMilliseconds,
                    PlanToken: planToken,
                    WithPlan: withPlan,
                    AllowDangerous: allowDangerous,
                    FailFast: failFast,
                    RequestJson: serviceRequestInputReadResult.Json!)
                {
                    AllowPlayMode = allowPlayMode,
                },
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = CallCommandResultFactory.Create(serviceResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    private int WriteRequestReadFailure (RequestInputReadResult requestInputReadResult)
    {
        var failureResult = CallFailureResultFactory.FromExecutionError(requestInputReadResult.Error!, output: null);
        var commandResult = CallCommandResultFactory.Create(failureResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
