using MackySoft.Ucli.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun.Pipeline;
using MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun.Preflight;
using MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun.Projection;

namespace MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun;

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
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the normalized service result. </returns>
    public async ValueTask<TestRunServiceResult> Execute (
        TestRunCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var preflightResult = await preflightService.Execute(input, cancellationToken).ConfigureAwait(false);
        if (!preflightResult.IsSuccess)
        {
            return preflightResult.Failure!;
        }

        var pipelineResult = await executionPipeline.Execute(
            preflightResult.Context!,
            cancellationToken).ConfigureAwait(false);
        return resultMapper.Map(pipelineResult);
    }
}
