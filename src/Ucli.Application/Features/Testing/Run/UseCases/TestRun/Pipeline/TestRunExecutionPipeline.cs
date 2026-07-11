using System.Text.Json;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;

/// <summary> Implements one test-run execution pipeline from artifacts preparation to conversion completion. </summary>
internal sealed class TestRunExecutionPipeline : ITestRunExecutionPipeline
{
    private readonly ITestRunArtifactsService artifactsService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    private readonly IUnityStreamingRequestExecutor unityStreamingRequestExecutor;

    private readonly IUnityResultsConverter resultsConverter;

    private readonly ITestRunArtifactExistenceProbe artifactExistenceProbe;

    /// <summary> Initializes a new instance of the <see cref="TestRunExecutionPipeline" /> class. </summary>
    /// <param name="artifactsService"> The test-run artifacts service dependency. </param>
    /// <param name="unityRequestExecutor"> The Unity request executor dependency. </param>
    /// <param name="resultsConverter"> The Unity results converter dependency. </param>
    /// <param name="artifactExistenceProbe"> The generated artifact existence probe dependency. </param>
    /// <param name="unityStreamingRequestExecutor"> The streaming-capable Unity request executor dependency. </param>
    public TestRunExecutionPipeline (
        ITestRunArtifactsService artifactsService,
        IUnityRequestExecutor unityRequestExecutor,
        IUnityResultsConverter resultsConverter,
        ITestRunArtifactExistenceProbe artifactExistenceProbe,
        IUnityStreamingRequestExecutor unityStreamingRequestExecutor)
    {
        this.artifactsService = artifactsService ?? throw new ArgumentNullException(nameof(artifactsService));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.resultsConverter = resultsConverter ?? throw new ArgumentNullException(nameof(resultsConverter));
        this.artifactExistenceProbe = artifactExistenceProbe ?? throw new ArgumentNullException(nameof(artifactExistenceProbe));
        this.unityStreamingRequestExecutor = unityStreamingRequestExecutor ?? throw new ArgumentNullException(nameof(unityStreamingRequestExecutor));
    }

    /// <summary> Executes one test-run pipeline from prepared configuration. </summary>
    /// <param name="context"> The preflight-resolved execution context. </param>
    /// <param name="progressSink"> The optional command-neutral sink that receives live progress entries. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to pipeline output values. </returns>
    public async ValueTask<TestRunExecutionPipelineResult> ExecuteAsync (
        TestRunExecutionContext context,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        var configuration = context.Configuration;

        var artifactsPreparationResult = await PrepareArtifactsSafelyAsync(configuration, cancellationToken).ConfigureAwait(false);
        if (!artifactsPreparationResult.IsSuccess)
        {
            return TestRunExecutionPipelineResult.Failure(
                artifactsPreparationResult.Error!,
                allowEmptyTestRun: context.AllowEmptyTestRun);
        }

        var artifactsSession = artifactsPreparationResult.Session!;
        if (progressSink is not null)
        {
            var progressStartResult = await EmitRunStartedSafelyAsync(
                    configuration,
                    artifactsSession,
                    progressSink,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!progressStartResult.IsSuccess)
            {
                return TestRunExecutionPipelineResult.Failure(
                    progressStartResult.Error!,
                    artifactsSession,
                    allowEmptyTestRun: context.AllowEmptyTestRun);
            }
        }

        var unityExecutionResult = await ExecuteUnitySafelyAsync(
            context,
            artifactsSession,
            progressSink,
            cancellationToken).ConfigureAwait(false);
        var conversionResult = UnityResultsConversionResult.Success(hasFailedTests: false);
        ExecutionError? conversionUnexpectedError = null;

        if (unityExecutionResult.IsSuccess
            || CanRecoverCompletedOneshotResults(unityExecutionResult, context.Target, artifactsSession))
        {
            try
            {
                conversionResult = await ConvertResultsSafelyAsync(artifactsSession, cancellationToken).ConfigureAwait(false);
                if (!unityExecutionResult.IsSuccess && conversionResult.IsSuccess)
                {
                    unityExecutionResult = UnityTestExecutionResult.Success(conversionResult.HasFailedTests ? 2 : 0);
                }
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
        var completionResult = await CompleteArtifactsSafelyAsync(
            configuration,
            artifactsSession,
            context.Target,
            CancellationToken.None).ConfigureAwait(false);
        if (conversionUnexpectedError is not null)
        {
            return TestRunExecutionPipelineResult.Failure(
                conversionUnexpectedError,
                artifactsSession,
                unityExecutionResult,
                conversionResult,
                allowEmptyTestRun: context.AllowEmptyTestRun);
        }

        if (!completionResult.IsSuccess)
        {
            return TestRunExecutionPipelineResult.Failure(
                completionResult.Error!,
                artifactsSession,
                unityExecutionResult,
                conversionResult,
                allowEmptyTestRun: context.AllowEmptyTestRun);
        }

        return TestRunExecutionPipelineResult.Success(
            artifactsSession,
            unityExecutionResult,
            conversionResult,
            allowEmptyTestRun: context.AllowEmptyTestRun);
    }

    /// <summary> Prepares artifacts session and maps unexpected exceptions into internal errors. </summary>
    /// <param name="context"> The resolved run context. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the artifact preparation result. </returns>
    private async ValueTask<ArtifactsPreparationResult> PrepareArtifactsSafelyAsync (
        ResolvedTestRunConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await artifactsService.PrepareAsync(configuration, cancellationToken).ConfigureAwait(false);
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
    /// <param name="target"> The execution target held fixed for this test run. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the artifact completion result. </returns>
    private async ValueTask<ArtifactsCompletionResult> CompleteArtifactsSafelyAsync (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        UnityExecutionTarget target,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await artifactsService.CompleteAsync(configuration, session, target, cancellationToken).ConfigureAwait(false);
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
    private async ValueTask<UnityTestExecutionResult> ExecuteUnitySafelyAsync (
        TestRunExecutionContext context,
        ArtifactsSession session,
        ICommandProgressSink? progressSink,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (progressSink is not null)
            {
                return await ExecuteStreamingUnityAsync(
                        context,
                        session,
                        progressSink,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return await ExecuteUnityIpcAsync(context, session, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.Canceled,
                "Unity test execution was canceled.");
        }
        catch (TestRunProgressProtocolException exception)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ProgressProtocolViolation,
                exception.Message,
                TestRunErrorCodes.UnityTestExecutionFailed);
        }
        catch (Exception exception)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.AbnormalExit,
                $"Unexpected error during Unity test execution: {exception.Message}");
        }
    }

    private async ValueTask<UnityTestExecutionResult> ExecuteStreamingUnityAsync (
        TestRunExecutionContext context,
        ArtifactsSession session,
        ICommandProgressSink progressSink,
        CancellationToken cancellationToken)
    {
        var requestResult = await unityStreamingRequestExecutor.ExecuteAsync(
                UcliCommandIds.TestRun,
                UnityExecutionTargetModeMapper.ToExplicitMode(context.Target),
                context.Timeout,
                context.Config,
                context.Configuration.UnityProject,
                CreateTestRunRequestPayload(context, session),
                (frame, progressCancellationToken) => ForwardTestRunProgressFrameAsync(
                    frame,
                    session.RunId,
                    progressSink,
                    progressCancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
        return CreateUnityExecutionResult(requestResult, session, context.Target);
    }

    private async ValueTask<UnityTestExecutionResult> ExecuteUnityIpcAsync (
        TestRunExecutionContext context,
        ArtifactsSession session,
        CancellationToken cancellationToken)
    {
        var requestResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.TestRun,
                UnityExecutionTargetModeMapper.ToExplicitMode(context.Target),
                context.Timeout,
                context.Config,
                context.Configuration.UnityProject,
                CreateTestRunRequestPayload(context, session),
                cancellationToken)
            .ConfigureAwait(false);
        return CreateUnityExecutionResult(requestResult, session, context.Target);
    }

    private static UnityRequestPayload CreateTestRunRequestPayload (
        TestRunExecutionContext context,
        ArtifactsSession session)
    {
        var configuration = context.Configuration;
        return new UnityRequestPayload.TestRun(
            TestPlatform: TestRunPlatformCodec.ToValue(configuration.TestPlatform),
            TestFilter: configuration.TestFilter,
            TestCategories: configuration.TestCategories,
            AssemblyNames: configuration.AssemblyNames,
            TestSettingsPath: configuration.TestSettingsPath,
            ResultsXmlPath: session.Paths.ResultsXmlPath,
            EditorLogPath: session.Paths.EditorLogPath,
            FailFast: context.FailFast,
            RunId: session.RunId);
    }

    private static async ValueTask ForwardTestRunProgressFrameAsync (
        UnityRequestProgressFrame frame,
        string expectedRunId,
        ICommandProgressSink progressSink,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRunId);
        ArgumentNullException.ThrowIfNull(progressSink);

        switch (frame.Event)
        {
            case TestRunProgressEventNames.RunStarted:
                await ForwardProgressPayloadAsync<TestRunStartedEntry>(frame, expectedRunId, progressSink, cancellationToken).ConfigureAwait(false);
                return;
            case TestRunProgressEventNames.CaseStarted:
                await ForwardProgressPayloadAsync<TestCaseStartedEntry>(frame, expectedRunId, progressSink, cancellationToken).ConfigureAwait(false);
                return;
            case TestRunProgressEventNames.CaseFinished:
                await ForwardProgressPayloadAsync<TestCaseFinishedEntry>(frame, expectedRunId, progressSink, cancellationToken).ConfigureAwait(false);
                return;
            case TestRunProgressEventNames.RunDiagnostic:
                await ForwardProgressPayloadAsync<TestRunDiagnosticEntry>(frame, expectedRunId, progressSink, cancellationToken).ConfigureAwait(false);
                return;
            default:
                throw new TestRunProgressProtocolException($"Unity test-run progress event is not supported: {frame.Event}.");
        }
    }

    private static async ValueTask ForwardProgressPayloadAsync<TPayload> (
        UnityRequestProgressFrame frame,
        string expectedRunId,
        ICommandProgressSink progressSink,
        CancellationToken cancellationToken)
        where TPayload : notnull
    {
        if (!IpcPayloadCodec.TryDeserialize<TPayload>(frame.Payload, out var payload, out var error))
        {
            throw new TestRunProgressProtocolException(
                $"Unity test-run progress payload is invalid for event '{frame.Event}'. {error}");
        }

        TestRunProgressPayloadValidator.Validate(frame.Event, payload, expectedRunId);
        await progressSink.OnEntryAsync(
                frame.Event,
                payload,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private UnityTestExecutionResult CreateUnityExecutionResult (
        UnityRequestExecutionResult requestResult,
        ArtifactsSession session,
        UnityExecutionTarget target)
    {
        if (!requestResult.IsSuccess)
        {
            return CreateRequestFailure(requestResult.FailureInfo!, target);
        }

        if (!TryDecodeTestRunResponse(
                requestResult.Response!,
                out var exitCode,
                out var errorCode,
                out var errorMessage))
        {
            return UnityTestExecutionResult.Failure(
                MapResponseFailureKind(errorCode, target),
                errorMessage!,
                errorCode);
        }

        var artifactsExistenceResult = artifactExistenceProbe.ValidateGeneratedFiles(session.Paths);
        if (!artifactsExistenceResult.IsSuccess)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ArtifactMissing,
                artifactsExistenceResult.ErrorMessage!);
        }

        return UnityTestExecutionResult.Success(exitCode);
    }

    private bool CanRecoverCompletedOneshotResults (
        UnityTestExecutionResult unityExecutionResult,
        UnityExecutionTarget target,
        ArtifactsSession session)
    {
        if (target != UnityExecutionTarget.Oneshot
            || unityExecutionResult.FailureKind != UnityTestExecutionFailureKind.AbnormalExit
            || unityExecutionResult.ErrorCode != UcliCoreErrorCodes.InternalError
            || string.IsNullOrWhiteSpace(unityExecutionResult.ErrorMessage))
        {
            return false;
        }

        // NOTE:
        // Unity Test Runner can close oneshot IPC during post-test domain reload after it has
        // already written complete results. Treat only that exact transport loss as recoverable;
        // all other abnormal exits preserve the primary execution failure.
        if (!unityExecutionResult.ErrorMessage.StartsWith(
                "Failed to execute Unity oneshot IPC request.",
                StringComparison.Ordinal)
            || !unityExecutionResult.ErrorMessage.Contains(
                "IPC stream ended before a complete frame was read.",
                StringComparison.Ordinal))
        {
            return false;
        }

        return artifactExistenceProbe.ValidateGeneratedFiles(session.Paths).IsSuccess;
    }

    private static UnityTestExecutionResult CreateRequestFailure (
        UnityRequestFailure failure,
        UnityExecutionTarget target)
    {
        return UnityTestExecutionResult.Failure(
            MapFailureKind(failure, target),
            failure.Message,
            failure.Code,
            failure.StartupFailure);
    }

    private static UnityTestExecutionFailureKind MapFailureKind (
        UnityRequestFailure failure,
        UnityExecutionTarget target)
    {
        var code = failure.Code;
        if (code == ExecutionErrorCodes.IpcTimeout)
        {
            return target == UnityExecutionTarget.Oneshot
                ? UnityTestExecutionFailureKind.ProcessTimedOut
                : UnityTestExecutionFailureKind.IpcTimedOut;
        }

        if (code == ExecutionErrorCodes.Canceled)
        {
            return UnityTestExecutionFailureKind.Canceled;
        }

        if (code == UnityExecutionModeDecisionErrorCodes.DaemonNotRunning)
        {
            return UnityTestExecutionFailureKind.StartFailed;
        }

        if ((code == UcliCoreErrorCodes.InternalError || code == UcliCoreErrorCodes.InvalidArgument)
            && target == UnityExecutionTarget.Daemon
            && failure.Message.StartsWith("Daemon session token could not be resolved.", StringComparison.Ordinal))
        {
            return UnityTestExecutionFailureKind.ClientSetupFailed;
        }

        return UnityTestExecutionFailureKind.AbnormalExit;
    }

    private static UnityTestExecutionFailureKind MapResponseFailureKind (
        UcliCode? errorCode,
        UnityExecutionTarget target)
    {
        if (errorCode is { IsValid: true } code)
        {
            if (code == IpcTransportErrorCodes.IpcTimeout || code == ExecutionErrorCodes.IpcTimeout)
            {
                return target == UnityExecutionTarget.Oneshot
                    ? UnityTestExecutionFailureKind.ProcessTimedOut
                    : UnityTestExecutionFailureKind.IpcTimedOut;
            }

            if (code == ExecutionErrorCodes.Canceled)
            {
                return UnityTestExecutionFailureKind.Canceled;
            }
        }

        return UnityTestExecutionFailureKind.AbnormalExit;
    }

    private static bool TryDecodeTestRunResponse (
        UnityRequestResponse response,
        out int exitCode,
        out UcliCode? errorCode,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.HasFailureStatus)
        {
            if (response.Errors.Count > 0)
            {
                var firstError = response.Errors[0];
                exitCode = default;
                errorCode = firstError.Code;
                errorMessage = $"Unity test run failed with error code '{firstError.Code}'. {firstError.Message}";
                return false;
            }

            exitCode = default;
            errorCode = null;
            errorMessage = $"Unity test run failed with status '{response.FailureStatus}'.";
            return false;
        }

        if (!TryReadExitCode(response.Payload, out exitCode, out var readError))
        {
            errorCode = null;
            errorMessage = $"Unity test run payload is invalid. {readError}";
            return false;
        }

        if (exitCode != 0 && exitCode != 2)
        {
            errorCode = null;
            errorMessage = $"Unity test run returned unsupported exit code: {exitCode}.";
            return false;
        }

        errorCode = null;
        errorMessage = null;
        return true;
    }

    private static bool TryReadExitCode (
        JsonElement payload,
        out int exitCode,
        out string? error)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            exitCode = default;
            error = "Response payload must be a JSON object.";
            return false;
        }

        if (!payload.TryGetProperty("exitCode", out var exitCodeElement))
        {
            exitCode = default;
            error = "Required property 'exitCode' is missing.";
            return false;
        }

        if (!exitCodeElement.TryGetInt32(out exitCode))
        {
            exitCode = default;
            error = "Property 'exitCode' must be an integer.";
            return false;
        }

        error = null;
        return true;
    }

    private static async ValueTask<ProgressEmissionResult> EmitRunStartedSafelyAsync (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        ICommandProgressSink progressSink,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await progressSink.OnEntryAsync(
                    TestRunProgressEventNames.RunStarted,
                    new TestRunStartedEntry(
                        session.RunId,
                        TestRunPlatformCodec.ToValue(configuration.TestPlatform),
                        configuration.TestFilter,
                        configuration.AssemblyNames,
                        configuration.TestCategories),
                    cancellationToken)
                .ConfigureAwait(false);
            return ProgressEmissionResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ProgressEmissionResult.Failure(ExecutionError.InternalError(
                $"Unexpected error during test-run progress emission: {exception.Message}"));
        }
    }

    private sealed record ProgressEmissionResult (ExecutionError? Error)
    {
        public bool IsSuccess => Error is null;

        public static ProgressEmissionResult Success ()
        {
            return new ProgressEmissionResult(Error: null);
        }

        public static ProgressEmissionResult Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new ProgressEmissionResult(error);
        }
    }

    /// <summary> Converts Unity result artifacts and maps unexpected exceptions into conversion failures. </summary>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to results conversion result. </returns>
    private async ValueTask<UnityResultsConversionResult> ConvertResultsSafelyAsync (
        ArtifactsSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await resultsConverter.ConvertAsync(session, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.Canceled,
                "Unity results conversion was canceled.");
        }
    }
}
