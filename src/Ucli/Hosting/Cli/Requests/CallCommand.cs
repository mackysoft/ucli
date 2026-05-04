using ConsoleAppFramework;
using MackySoft.Ucli.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Features.Requests.Call.UseCases.Call.Preflight;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the call CLI command entry point. </summary>
internal sealed class CallCommand
{
    private readonly ICallService callService;

    private readonly ICallCommandPreflightService callCommandPreflightService;

    /// <summary> Initializes a new instance of the CallCommand class. </summary>
    /// <param name="callService"> The call workflow service dependency. </param>
    /// <param name="callCommandPreflightService"> The command preflight dependency used to preserve the call payload on option failures. </param>
    public CallCommand (
        ICallService callService,
        ICallCommandPreflightService callCommandPreflightService)
    {
        this.callService = callService ?? throw new ArgumentNullException(nameof(callService));
        this.callCommandPreflightService = callCommandPreflightService ?? throw new ArgumentNullException(nameof(callCommandPreflightService));
    }

    /// <summary> Executes the call command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="planToken">--planToken, Optional plan token issued by a prior plan execution.</param>
    /// <param name="withPlan">--withPlan, Includes one plan-equivalent payload alongside call output.</param>
    /// <param name="allowDangerous">--allowDangerous, Explicitly allows dangerous operations when the project config also permits them.</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet ready.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Call)]
    public async Task<int> Call (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? planToken = null,
        bool withPlan = false,
        bool allowDangerous = false,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            var preflightResult = await callCommandPreflightService.Prepare(
                    projectPath,
                    cancellationToken)
                .ConfigureAwait(false);
            var failureResult = preflightResult.ToFailureResult(normalizedTimeoutResult.Error!);
            var commandFailureResult = CallCommandResultFactory.Create(failureResult);
            CommandResultWriter.WriteToStandardOutput(commandFailureResult);
            return commandFailureResult.ExitCode;
        }

        var normalizedModeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!normalizedModeResult.IsSuccess)
        {
            var preflightResult = await callCommandPreflightService.Prepare(
                    projectPath,
                    cancellationToken)
                .ConfigureAwait(false);
            var failureResult = preflightResult.ToFailureResult(normalizedModeResult.Error!);
            var commandFailureResult = CallCommandResultFactory.Create(failureResult);
            CommandResultWriter.WriteToStandardOutput(commandFailureResult);
            return commandFailureResult.ExitCode;
        }

        var serviceResult = await callService.Execute(
                new CallCommandInput(
                    ProjectPath: projectPath,
                    Mode: normalizedModeResult.Mode,
                    TimeoutMilliseconds: normalizedTimeoutResult.TimeoutMilliseconds,
                    PlanToken: planToken,
                    WithPlan: withPlan,
                    AllowDangerous: allowDangerous,
                    FailFast: failFast),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = CallCommandResultFactory.Create(serviceResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
