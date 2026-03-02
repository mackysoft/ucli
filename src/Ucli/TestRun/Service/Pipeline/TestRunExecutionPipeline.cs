using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.TestRun.Execution;
using MackySoft.Ucli.TestRun.Results;

namespace MackySoft.Ucli.TestRun.Service.Pipeline;

/// <summary> Implements one test-run execution pipeline from artifacts preparation to conversion completion. </summary>
internal sealed class TestRunExecutionPipeline : ITestRunExecutionPipeline
{
    private readonly ITestRunArtifactsService artifactsService;

    private readonly IUnityTestExecutor unityTestExecutor;

    private readonly IUnityResultsConverter resultsConverter;

    /// <summary> Initializes a new instance of the <see cref="TestRunExecutionPipeline" /> class. </summary>
    /// <param name="artifactsService"> The test-run artifacts service dependency. </param>
    /// <param name="unityTestExecutor"> The Unity test executor dependency. </param>
    /// <param name="resultsConverter"> The Unity results converter dependency. </param>
    public TestRunExecutionPipeline (
        ITestRunArtifactsService artifactsService,
        IUnityTestExecutor unityTestExecutor,
        IUnityResultsConverter resultsConverter)
    {
        this.artifactsService = artifactsService ?? throw new ArgumentNullException(nameof(artifactsService));
        this.unityTestExecutor = unityTestExecutor ?? throw new ArgumentNullException(nameof(unityTestExecutor));
        this.resultsConverter = resultsConverter ?? throw new ArgumentNullException(nameof(resultsConverter));
    }

    /// <summary> Executes one test-run pipeline from prepared configuration. </summary>
    /// <param name="configuration"> The preflight-resolved configuration. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to pipeline output values. </returns>
    public async ValueTask<TestRunExecutionPipelineResult> Execute (
        ResolvedTestRunConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(configuration);

        var artifactsPreparationResult = PrepareArtifactsSafely(configuration);
        if (!artifactsPreparationResult.IsSuccess)
        {
            return TestRunExecutionPipelineResult.Failure(artifactsPreparationResult.Error!);
        }

        var artifactsSession = artifactsPreparationResult.Session!;
        var unityExecutionResult = await ExecuteUnitySafely(configuration, artifactsSession, cancellationToken).ConfigureAwait(false);
        var conversionResult = UnityResultsConversionResult.Success(hasFailedTests: false);

        if (unityExecutionResult.IsSuccess)
        {
            conversionResult = await ConvertResultsSafely(artifactsSession, cancellationToken).ConfigureAwait(false);
        }

        var completionResult = CompleteArtifactsSafely(configuration, artifactsSession);
        if (!completionResult.IsSuccess)
        {
            return TestRunExecutionPipelineResult.Failure(
                completionResult.Error!,
                artifactsSession,
                unityExecutionResult,
                conversionResult);
        }

        return TestRunExecutionPipelineResult.Success(
            artifactsSession,
            unityExecutionResult,
            conversionResult);
    }

    /// <summary> Prepares artifacts session and maps unexpected exceptions into internal errors. </summary>
    /// <param name="configuration"> The resolved run configuration. </param>
    /// <returns> The artifact preparation result. </returns>
    private ArtifactsPreparationResult PrepareArtifactsSafely (ResolvedTestRunConfiguration configuration)
    {
        try
        {
            return artifactsService.Prepare(configuration);
        }
        catch (Exception exception)
        {
            return ArtifactsPreparationResult.Failure(ExecutionError.InternalError(
                $"Unexpected error during artifacts preparation: {exception.Message}"));
        }
    }

    /// <summary> Completes artifacts session and maps unexpected exceptions into internal errors. </summary>
    /// <param name="configuration"> The resolved run configuration. </param>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <returns> The artifact completion result. </returns>
    private ArtifactsCompletionResult CompleteArtifactsSafely (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session)
    {
        try
        {
            return artifactsService.Complete(configuration, session);
        }
        catch (Exception exception)
        {
            return ArtifactsCompletionResult.Failure(ExecutionError.InternalError(
                $"Unexpected error during artifacts completion: {exception.Message}"));
        }
    }

    /// <summary> Executes Unity tests and maps unexpected exceptions into tool failures. </summary>
    /// <param name="configuration"> The resolved run configuration. </param>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to Unity execution result. </returns>
    private async ValueTask<UnityTestExecutionResult> ExecuteUnitySafely (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await unityTestExecutor.Execute(configuration, session.Paths, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.Canceled,
                "Unity test execution was canceled.");
        }
        catch (Exception exception)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.AbnormalExit,
                $"Unexpected error during Unity test execution: {exception.Message}");
        }
    }

    /// <summary> Converts Unity result artifacts and maps unexpected exceptions into conversion failures. </summary>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to results conversion result. </returns>
    private async ValueTask<UnityResultsConversionResult> ConvertResultsSafely (
        ArtifactsSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await resultsConverter.Convert(session, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.Canceled,
                "Unity results conversion was canceled.");
        }
        catch (Exception exception)
        {
            return UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.InvalidResultsXml,
                $"Unexpected error during Unity results conversion: {exception.Message}");
        }
    }
}