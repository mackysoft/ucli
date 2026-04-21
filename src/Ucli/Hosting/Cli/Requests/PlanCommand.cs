using ConsoleAppFramework;
using MackySoft.Ucli.Features.Requests.Plan.UseCases.Plan;
using MackySoft.Ucli.Features.Requests.Plan.UseCases.Plan.Preflight;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the <c>plan</c> CLI command entry point. </summary>
internal sealed class PlanCommand
{
    private readonly IPlanService planService;

    private readonly IPlanCommandPreflightService planCommandPreflightService;

    /// <summary> Initializes a new instance of the <see cref="PlanCommand" /> class. </summary>
    /// <param name="planService"> The plan workflow service dependency. </param>
    /// <param name="planCommandPreflightService"> The command preflight dependency used to preserve the plan payload on option failures. </param>
    public PlanCommand (
        IPlanService planService,
        IPlanCommandPreflightService planCommandPreflightService)
    {
        this.planService = planService ?? throw new ArgumentNullException(nameof(planService));
        this.planCommandPreflightService = planCommandPreflightService ?? throw new ArgumentNullException(nameof(planCommandPreflightService));
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

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            var preflightResult = await planCommandPreflightService.Prepare(
                    requestPath,
                    projectPath,
                    normalizedReadIndexModeResult.Mode,
                    cancellationToken)
                .ConfigureAwait(false);
            var failureResult = preflightResult.ToFailureResult(normalizedTimeoutResult.Error!);
            var commandFailureResult = PlanCommandResultFactory.Create(failureResult);
            CommandResultWriter.WriteToStandardOutput(commandFailureResult);
            return commandFailureResult.ExitCode;
        }

        var normalizedModeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!normalizedModeResult.IsSuccess)
        {
            var preflightResult = await planCommandPreflightService.Prepare(
                    requestPath,
                    projectPath,
                    normalizedReadIndexModeResult.Mode,
                    cancellationToken)
                .ConfigureAwait(false);
            var failureResult = preflightResult.ToFailureResult(normalizedModeResult.Error!);
            var commandFailureResult = PlanCommandResultFactory.Create(failureResult);
            CommandResultWriter.WriteToStandardOutput(commandFailureResult);
            return commandFailureResult.ExitCode;
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