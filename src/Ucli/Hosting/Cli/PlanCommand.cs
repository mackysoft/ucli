using ConsoleAppFramework;
using MackySoft.Ucli.Features.Requests.Plan;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli;

/// <summary> Provides the <c>plan</c> CLI command entry point. </summary>
internal sealed class PlanCommand
{
    private readonly IPlanService planService;

    /// <summary> Initializes a new instance of the <see cref="PlanCommand" /> class. </summary>
    /// <param name="planService"> The plan workflow service dependency. </param>
    public PlanCommand (IPlanService planService)
    {
        this.planService = planService ?? throw new ArgumentNullException(nameof(planService));
    }

    /// <summary> Executes the <c>plan</c> command and emits the JSON result contract. </summary>
    /// <param name="requestPath">--requestPath, Optional path to one request JSON file.</param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (<c>auto|daemon|oneshot</c>).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="readIndexMode">--readIndexMode, readIndex mode (<c>disabled|allowStale|requireFresh</c>).</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet <c>ready</c>.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Plan)]
    public async Task<int> Plan (
        string? requestPath = null,
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? readIndexMode = null,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var normalizedReadIndexModeResult = ReadIndexModeOptionNormalizer.Normalize(readIndexMode);
        if (!normalizedReadIndexModeResult.IsSuccess)
        {
            var errorResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.Plan,
                normalizedReadIndexModeResult.Error!);
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedModeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!normalizedModeResult.IsSuccess)
        {
            var errorResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.Plan,
                normalizedModeResult.Error!);
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            var errorResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.Plan,
                normalizedTimeoutResult.Error!);
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var serviceResult = await planService.Execute(
                new PlanCommandInput(
                    RequestPath: requestPath,
                    ProjectPath: projectPath,
                    Mode: normalizedModeResult.Mode,
                    TimeoutMilliseconds: normalizedTimeoutResult.TimeoutMilliseconds,
                    ReadIndexMode: normalizedReadIndexModeResult.Mode,
                    FailFast: failFast),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = PlanCommandResultFactory.Create(serviceResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}