using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Preflight;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Projection;
using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;

/// <summary> Implements the core test-run orchestration flow. </summary>
internal sealed class TestRunService : ITestRunService
{
    private readonly ITestRunPreflightService preflightService;

    private readonly ITestRunExecutionPipeline executionPipeline;

    private readonly ITestRunResultMapper resultMapper;

    /// <summary> Initializes a new instance of the <see cref="TestRunService" /> class with explicit split components. </summary>
    /// <param name="preflightService"> The preflight service dependency. </param>
    /// <param name="executionPipeline"> The execution pipeline dependency. </param>
    /// <param name="resultMapper"> The result mapper dependency. </param>
    public TestRunService (
        ITestRunPreflightService preflightService,
        ITestRunExecutionPipeline executionPipeline,
        ITestRunResultMapper resultMapper)
    {
        this.preflightService = preflightService ?? throw new ArgumentNullException(nameof(preflightService));
        this.executionPipeline = executionPipeline ?? throw new ArgumentNullException(nameof(executionPipeline));
        this.resultMapper = resultMapper ?? throw new ArgumentNullException(nameof(resultMapper));
    }

    /// <summary> Executes one core test-run flow. </summary>
    /// <param name="input"> The interpreted command input values. </param>
    /// <param name="progressSink"> The optional command-neutral sink that receives live progress entries. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the normalized service result. </returns>
    public async ValueTask<TestRunServiceResult> ExecuteAsync (
        TestRunCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var preflightResult = await preflightService.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
        if (preflightResult is TestRunPreflightResult.TestRunPreflightFailure preflightFailure)
        {
            return preflightFailure.CommandError;
        }

        var preflightSuccess = preflightResult as TestRunPreflightResult.TestRunPreflightSuccess
            ?? throw new ArgumentOutOfRangeException(
                nameof(preflightResult),
                preflightResult.GetType(),
                "Test Run preflight result type must have an explicit service projection.");
        var pipelineResult = await executionPipeline.ExecuteAsync(
            preflightSuccess.Context,
            progressSink,
            cancellationToken).ConfigureAwait(false);
        return resultMapper.Map(pipelineResult);
    }
}
