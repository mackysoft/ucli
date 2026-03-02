using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.TestRun.Execution;
using MackySoft.Ucli.TestRun.Results;

namespace MackySoft.Ucli.TestRun.Service;

/// <summary> Implements the core test-run flow for configuration resolution, mode decision, execution, and result conversion. </summary>
internal sealed class TestRunService : ITestRunService
{
    private readonly ITestRunConfigurationResolver configurationResolver;

    private readonly IUcliConfigStore configStore;

    private readonly IUnityExecutionModeDecisionService modeDecisionService;

    private readonly ITestRunArtifactsService artifactsService;

    private readonly IUnityTestExecutor unityTestExecutor;

    private readonly IUnityResultsConverter resultsConverter;

    /// <summary> Initializes a new instance of the <see cref="TestRunService" /> class. </summary>
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
    {
        this.configurationResolver = configurationResolver ?? throw new ArgumentNullException(nameof(configurationResolver));
        this.configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        this.modeDecisionService = modeDecisionService ?? throw new ArgumentNullException(nameof(modeDecisionService));
        this.artifactsService = artifactsService ?? throw new ArgumentNullException(nameof(artifactsService));
        this.unityTestExecutor = unityTestExecutor ?? throw new ArgumentNullException(nameof(unityTestExecutor));
        this.resultsConverter = resultsConverter ?? throw new ArgumentNullException(nameof(resultsConverter));
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

        var configurationResolutionResult = ResolveConfigurationSafely(input);
        if (!configurationResolutionResult.IsSuccess)
        {
            return CreateConfigurationFailureResult(configurationResolutionResult.Errors);
        }

        var configuration = configurationResolutionResult.Configuration!;

        var configLoadResult = await configStore.Load(
            configuration.UnityProject.RepositoryRoot,
            cancellationToken).ConfigureAwait(false);
        if (!configLoadResult.IsSuccess)
        {
            return CreateErrorFromExecutionError(configLoadResult.Error!);
        }

        var modeDecisionResult = await modeDecisionService.Decide(
            commandName: UcliCommandNames.Test,
            mode: configuration.Mode,
            timeout: null,
            config: configLoadResult.Config!,
            unityProject: configuration.UnityProject,
            cancellationToken).ConfigureAwait(false);
        if (modeDecisionResult.HasContractError)
        {
            return TestRunServiceResult.InfraError(
                modeDecisionResult.ContractError!.Message,
                modeDecisionResult.ContractError.Code);
        }

        if (!modeDecisionResult.IsSuccess)
        {
            return CreateErrorFromExecutionError(modeDecisionResult.Error!);
        }

        if (modeDecisionResult.Decision!.Target == UnityExecutionTarget.Daemon)
        {
            return TestRunServiceResult.ToolError(
                "Daemon path is not supported by test run core service.",
                TestRunErrorCodes.TestRunDaemonPathUnsupported);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TestRunServiceResult.ToolError(
                "Unity test execution was canceled.",
                CliErrorCodes.Canceled);
        }

        var artifactsPreparationResult = PrepareArtifactsSafely(configuration);
        if (!artifactsPreparationResult.IsSuccess)
        {
            return CreateErrorFromExecutionError(artifactsPreparationResult.Error!);
        }

        var artifactsSession = artifactsPreparationResult.Session!;
        var unityExecutionResult = await ExecuteUnitySafely(configuration, artifactsSession, cancellationToken).ConfigureAwait(false);
        var conversionResult = UnityResultsConversionResult.Success(hasFailedTests: false);

        if (unityExecutionResult.IsSuccess)
        {
            conversionResult = await ConvertResultsSafely(artifactsSession, cancellationToken).ConfigureAwait(false);
        }

        var output = CreateExecutionResult(unityExecutionResult, conversionResult, artifactsSession);

        var completionResult = CompleteArtifactsSafely(configuration, artifactsSession);
        if (!completionResult.IsSuccess)
        {
            output = CreateErrorFromExecutionError(
                completionResult.Error!,
                artifactsSession.RunId,
                artifactsSession.ArtifactsDir,
                artifactsSession.Paths.SummaryJsonPath);
        }

        return output;
    }

    /// <summary> Resolves run configuration and converts unexpected exceptions into structured failures. </summary>
    /// <param name="input"> The raw command input values. </param>
    /// <returns> The configuration resolution result. </returns>
    private TestRunConfigurationResolutionResult ResolveConfigurationSafely (TestRunCommandInput input)
    {
        try
        {
            return configurationResolver.Resolve(input);
        }
        catch (Exception exception)
        {
            return TestRunConfigurationResolutionResult.Failure(
            [
                ExecutionError.InternalError($"Unexpected error while resolving run configuration: {exception.Message}"),
            ]);
        }
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

    /// <summary> Creates final output from execution and conversion outcomes. </summary>
    /// <param name="unityExecutionResult"> The Unity execution result. </param>
    /// <param name="conversionResult"> The results conversion result. </param>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <returns> The final service result. </returns>
    private static TestRunServiceResult CreateExecutionResult (
        UnityTestExecutionResult unityExecutionResult,
        UnityResultsConversionResult conversionResult,
        ArtifactsSession session)
    {
        if (!unityExecutionResult.IsSuccess)
        {
            var errorCode = unityExecutionResult.FailureKind == UnityTestExecutionFailureKind.Canceled
                ? CliErrorCodes.Canceled
                : TestRunErrorCodes.UnityTestExecutionFailed;

            return TestRunServiceResult.ToolError(
                unityExecutionResult.ErrorMessage ?? "Unity test execution failed.",
                errorCode,
                runId: session.RunId,
                artifactsDir: session.ArtifactsDir,
                summaryJsonPath: session.Paths.SummaryJsonPath);
        }

        if (!conversionResult.IsSuccess)
        {
            return conversionResult.FailureKind switch
            {
                UnityResultsConversionFailureKind.OutputWriteFailed => TestRunServiceResult.InfraError(
                    conversionResult.ErrorMessage ?? "Failed to write test result artifacts.",
                    TestRunErrorCodes.TestResultsOutputWriteFailed,
                    runId: session.RunId,
                    artifactsDir: session.ArtifactsDir,
                    summaryJsonPath: session.Paths.SummaryJsonPath),
                UnityResultsConversionFailureKind.Canceled => TestRunServiceResult.ToolError(
                    conversionResult.ErrorMessage ?? "Unity results conversion was canceled.",
                    CliErrorCodes.Canceled,
                    runId: session.RunId,
                    artifactsDir: session.ArtifactsDir,
                    summaryJsonPath: session.Paths.SummaryJsonPath),
                _ => TestRunServiceResult.ToolError(
                    conversionResult.ErrorMessage ?? "Unity results XML is invalid.",
                    TestRunErrorCodes.TestResultsXmlInvalid,
                    runId: session.RunId,
                    artifactsDir: session.ArtifactsDir,
                    summaryJsonPath: session.Paths.SummaryJsonPath),
            };
        }

        if (conversionResult.HasFailedTests)
        {
            return TestRunServiceResult.Fail(
                message: "Unity test execution completed with failed tests.",
                runId: session.RunId,
                artifactsDir: session.ArtifactsDir,
                summaryJsonPath: session.Paths.SummaryJsonPath);
        }

        return TestRunServiceResult.Pass(
            message: "Unity test execution completed.",
            runId: session.RunId,
            artifactsDir: session.ArtifactsDir,
            summaryJsonPath: session.Paths.SummaryJsonPath);
    }

    /// <summary> Creates configuration resolution failure output from structured errors. </summary>
    /// <param name="errors"> The configuration resolution errors. </param>
    /// <returns> The failure service result. </returns>
    private static TestRunServiceResult CreateConfigurationFailureResult (IReadOnlyList<ExecutionError> errors)
    {
        if (errors.Count == 0)
        {
            return TestRunServiceResult.InfraError(
                "Unexpected error while resolving run configuration.",
                IpcErrorCodes.InternalError);
        }

        var hasInternalError = errors.Any(static error => error.Kind == ExecutionErrorKind.InternalError);
        var errorCode = hasInternalError
            ? IpcErrorCodes.InternalError
            : IpcErrorCodes.InvalidArgument;
        var message = string.Join(" | ", errors.Select(static error => error.Message));

        return hasInternalError
            ? TestRunServiceResult.InfraError(message, errorCode)
            : TestRunServiceResult.InvalidInput(message, errorCode);
    }

    /// <summary> Converts execution errors into service results. </summary>
    /// <param name="error"> The execution error. </param>
    /// <param name="runId"> The optional run identifier. </param>
    /// <param name="artifactsDir"> The optional artifacts directory path. </param>
    /// <param name="summaryJsonPath"> The optional summary JSON path. </param>
    /// <returns> The mapped service result. </returns>
    private static TestRunServiceResult CreateErrorFromExecutionError (
        ExecutionError error,
        string? runId = null,
        string? artifactsDir = null,
        string? summaryJsonPath = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => TestRunServiceResult.InvalidInput(
                error.Message,
                IpcErrorCodes.InvalidArgument,
                runId,
                artifactsDir,
                summaryJsonPath),
            ExecutionErrorKind.Timeout => TestRunServiceResult.ToolError(
                error.Message,
                CliErrorCodes.IpcTimeout,
                runId,
                artifactsDir,
                summaryJsonPath),
            _ => TestRunServiceResult.InfraError(
                error.Message,
                IpcErrorCodes.InternalError,
                runId,
                artifactsDir,
                summaryJsonPath),
        };
    }
}