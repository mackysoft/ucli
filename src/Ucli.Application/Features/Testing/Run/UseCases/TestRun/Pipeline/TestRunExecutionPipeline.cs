using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Preflight;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;

/// <summary> Implements one test-run execution pipeline from artifacts preparation to conversion completion. </summary>
internal sealed class TestRunExecutionPipeline : ITestRunExecutionPipeline
{
    private readonly ITestRunArtifactsService artifactsService;

    private readonly IUnityTestExecutor unityTestExecutor;

    private readonly IDaemonTestRunClient daemonTestRunClient;

    private readonly IUnityResultsConverter resultsConverter;

    /// <summary> Initializes a new instance of the <see cref="TestRunExecutionPipeline" /> class. </summary>
    /// <param name="artifactsService"> The test-run artifacts service dependency. </param>
    /// <param name="unityTestExecutor"> The Unity test executor dependency. </param>
    /// <param name="resultsConverter"> The Unity results converter dependency. </param>
    public TestRunExecutionPipeline (
        ITestRunArtifactsService artifactsService,
        IUnityTestExecutor unityTestExecutor,
        IDaemonTestRunClient daemonTestRunClient,
        IUnityResultsConverter resultsConverter)
    {
        this.artifactsService = artifactsService ?? throw new ArgumentNullException(nameof(artifactsService));
        this.unityTestExecutor = unityTestExecutor ?? throw new ArgumentNullException(nameof(unityTestExecutor));
        this.daemonTestRunClient = daemonTestRunClient ?? throw new ArgumentNullException(nameof(daemonTestRunClient));
        this.resultsConverter = resultsConverter ?? throw new ArgumentNullException(nameof(resultsConverter));
    }

    /// <summary> Executes one test-run pipeline from prepared configuration. </summary>
    /// <param name="context"> The preflight-resolved execution context. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to pipeline output values. </returns>
    public async ValueTask<TestRunExecutionPipelineResult> Execute (
        TestRunExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        var configuration = context.Configuration;

        var artifactsPreparationResult = await PrepareArtifactsSafely(configuration, cancellationToken).ConfigureAwait(false);
        if (!artifactsPreparationResult.IsSuccess)
        {
            return TestRunExecutionPipelineResult.Failure(artifactsPreparationResult.Error!);
        }

        var artifactsSession = artifactsPreparationResult.Session!;
        var unityExecutionResult = await ExecuteUnitySafely(
            context,
            artifactsSession,
            cancellationToken).ConfigureAwait(false);
        var conversionResult = UnityResultsConversionResult.Success(hasFailedTests: false);
        ExecutionError? conversionUnexpectedError = null;

        if (unityExecutionResult.IsSuccess)
        {
            try
            {
                conversionResult = await ConvertResultsSafely(artifactsSession, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                conversionUnexpectedError = ExecutionError.InternalError(
                    $"Unexpected error during Unity results conversion: {exception.Message}");
            }
        }

        // NOTE:
        // Completion metadata must be written even when caller cancellation is requested,
        // so mapping can preserve run-scoped diagnostics.
        var completionResult = await CompleteArtifactsSafely(
            configuration,
            artifactsSession,
            CancellationToken.None).ConfigureAwait(false);
        if (conversionUnexpectedError is not null)
        {
            return TestRunExecutionPipelineResult.Failure(
                conversionUnexpectedError,
                artifactsSession,
                unityExecutionResult,
                conversionResult);
        }

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
    /// <param name="context"> The resolved run context. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the artifact preparation result. </returns>
    private async ValueTask<ArtifactsPreparationResult> PrepareArtifactsSafely (
        ResolvedTestRunConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await artifactsService.Prepare(configuration, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the artifact completion result. </returns>
    private async ValueTask<ArtifactsCompletionResult> CompleteArtifactsSafely (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await artifactsService.Complete(configuration, session, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
        TestRunExecutionContext context,
        ArtifactsSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return context.Target switch
            {
                UnityExecutionTarget.Daemon => await daemonTestRunClient.Execute(
                        context.Configuration,
                        session.Paths,
                        context.Timeout,
                        context.FailFast,
                        cancellationToken)
                    .ConfigureAwait(false),
                UnityExecutionTarget.Oneshot => await unityTestExecutor.Execute(
                        context.Configuration,
                        session.Paths,
                        context.Timeout,
                        cancellationToken)
                    .ConfigureAwait(false),
                _ => UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.StartFailed,
                    $"Unsupported Unity execution target: {context.Target}."),
            };
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
    }
}
