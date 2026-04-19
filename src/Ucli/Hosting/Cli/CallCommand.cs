using ConsoleAppFramework;
using MackySoft.Ucli.Features.Requests.Call;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli;

/// <summary> Provides the <c>call</c> CLI command entry point. </summary>
internal sealed class CallCommand
{
    private readonly ICallService callService;

    /// <summary> Initializes a new instance of the <see cref="CallCommand" /> class. </summary>
    /// <param name="callService"> The call workflow service dependency. </param>
    public CallCommand (ICallService callService)
    {
        this.callService = callService ?? throw new ArgumentNullException(nameof(callService));
    }

    /// <summary> Executes the <c>call</c> command and emits the JSON result contract. </summary>
    /// <param name="requestPath">--requestPath, Optional path to one request JSON file.</param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (<c>auto|daemon|oneshot</c>).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="planToken">--planToken, Optional plan token issued by a prior <c>plan</c> execution.</param>
    /// <param name="withPlan">--withPlan, Includes one plan-equivalent payload alongside call output.</param>
    /// <param name="allowDangerous">--allowDangerous, Explicitly allows dangerous operations when the project config also permits them.</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet <c>ready</c>.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Call)]
    public async Task<int> Call (
        string? requestPath = null,
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

        var normalizedModeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!normalizedModeResult.IsSuccess)
        {
            var errorResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.Call,
                normalizedModeResult.Error!);
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            var errorResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.Call,
                normalizedTimeoutResult.Error!);
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var serviceResult = await callService.Execute(
                new CallCommandInput(
                    RequestPath: requestPath,
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