using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan.Projection;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;
using MackySoft.Ucli.Hosting.Cli.Requests.Input;
using MackySoft.Ucli.Hosting.Cli.Requests.Plan.Preflight;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the plan CLI command entry point. </summary>
internal sealed class PlanCommand
{
    private readonly IPlanService planService;

    private readonly IPlanCommandPreflightService planCommandPreflightService;

    private readonly IRequestInputReader requestInputReader;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the PlanCommand class. </summary>
    /// <param name="planService"> The plan workflow service dependency. </param>
    /// <param name="planCommandPreflightService"> The command preflight dependency used to preserve the plan payload on option failures. </param>
    /// <param name="requestInputReader"> The CLI request-input reader dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public PlanCommand (
        IPlanService planService,
        IPlanCommandPreflightService planCommandPreflightService,
        IRequestInputReader requestInputReader,
        ICommandResultWriter commandResultWriter)
    {
        this.planService = planService ?? throw new ArgumentNullException(nameof(planService));
        this.planCommandPreflightService = planCommandPreflightService ?? throw new ArgumentNullException(nameof(planCommandPreflightService));
        this.requestInputReader = requestInputReader ?? throw new ArgumentNullException(nameof(requestInputReader));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the plan command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="readIndexMode">--readIndexMode, readIndex mode (disabled|allowStale|requireFresh).</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet ready.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Plan)]
    public async Task<int> PlanAsync (
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
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            var requestInputReadResult = await requestInputReader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (!requestInputReadResult.IsSuccess)
            {
                return WriteRequestReadFailure(requestInputReadResult);
            }

            var preflightResult = await planCommandPreflightService.PrepareAsync(
                    projectPath,
                    requestInputReadResult.Json!,
                    normalizedReadIndexModeResult.Mode,
                    cancellationToken)
                .ConfigureAwait(false);
            var failureResult = preflightResult.ToFailureResult(normalizedTimeoutResult.Error!);
            var commandFailureResult = PlanCommandResultFactory.Create(failureResult);
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

            var preflightResult = await planCommandPreflightService.PrepareAsync(
                    projectPath,
                    requestInputReadResult.Json!,
                    normalizedReadIndexModeResult.Mode,
                    cancellationToken)
                .ConfigureAwait(false);
            var failureResult = preflightResult.ToFailureResult(normalizedModeResult.Error!);
            var commandFailureResult = PlanCommandResultFactory.Create(failureResult);
            commandResultWriter.WriteToStandardOutput(commandFailureResult);
            return commandFailureResult.ExitCode;
        }

        var serviceRequestInputReadResult = await requestInputReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (!serviceRequestInputReadResult.IsSuccess)
        {
            return WriteRequestReadFailure(serviceRequestInputReadResult);
        }

        var serviceResult = await planService.ExecuteAsync(
                new PlanCommandInput(
                    ProjectPath: projectPath,
                    Mode: normalizedModeResult.Mode,
                    TimeoutMilliseconds: normalizedTimeoutResult.TimeoutMilliseconds,
                    ReadIndexMode: normalizedReadIndexModeResult.Mode,
                    FailFast: failFast,
                    RequestJson: serviceRequestInputReadResult.Json!),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = PlanCommandResultFactory.Create(serviceResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    private int WriteRequestReadFailure (RequestInputReadResult requestInputReadResult)
    {
        var failureResult = PlanFailureResultFactory.FromExecutionError(requestInputReadResult.Error!);
        var commandResult = PlanCommandResultFactory.Create(failureResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
