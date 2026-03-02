using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.TestRun.Execution;
using MackySoft.Ucli.TestRun.Results;
using MackySoft.Ucli.TestRun.Service.Mapping;
using MackySoft.Ucli.TestRun.Service.Pipeline;
using MackySoft.Ucli.TestRun.Service.Preflight;

namespace MackySoft.Ucli.TestRun.Service;

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

    /// <summary> Initializes a new instance of the <see cref="TestRunService" /> class from legacy dependencies. </summary>
    /// <param name="configurationResolver"> The test-run configuration resolver dependency. </param>
    /// <param name="configStore"> The uCLI config store dependency. </param>
    /// <param name="modeDecisionService"> The Unity execution mode decision service dependency. </param>
    /// <param name="artifactsService"> The test-run artifacts service dependency. </param>
    /// <param name="unityTestExecutor"> The Unity test executor dependency. </param>
    /// <param name="resultsConverter"> The Unity results converter dependency. </param>
    public TestRunService (
        ITestRunConfigurationResolver configurationResolver,
        IUcliConfigStore configStore,
        IUnityExecutionModeDecisionService modeDecisionService,
        ITestRunArtifactsService artifactsService,
        IUnityTestExecutor unityTestExecutor,
        IUnityResultsConverter resultsConverter)
        : this(
            new TestRunPreflightService(configurationResolver, configStore, modeDecisionService),
            new TestRunExecutionPipeline(artifactsService, unityTestExecutor, resultsConverter),
            new TestRunResultMapper())
    {
    }

    /// <summary> Executes one core test-run flow. </summary>
    /// <param name="input"> The raw command input values. </param>
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
            preflightResult.Configuration!,
            cancellationToken).ConfigureAwait(false);
        return resultMapper.Map(pipelineResult);
    }
}