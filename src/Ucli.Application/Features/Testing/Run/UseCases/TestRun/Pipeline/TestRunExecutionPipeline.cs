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
            return TestRunExecutionPipelineResult.FailedBeforeArtifacts(
                artifactsPreparationResult.Error!);
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
                return TestRunExecutionPipelineResult.FailedAfterArtifacts(
                    artifactsSession,
                    ApplicationFailure.FromExecutionError(progressStartResult.Error!));
            }
        }

        var unityExecutionResult = await ExecuteUnitySafelyAsync(
            context,
            artifactsSession,
            progressSink,
            cancellationToken).ConfigureAwait(false);
        UnityResultsConversionSuccess? conversionSuccess = null;
        ApplicationFailure? primaryFailure = null;
        if (unityExecutionResult is not UnityTestExecutionResult.ExecutionFailure executionFailure
            || CanRecoverCompletedOneshotResults(executionFailure, context.Target, artifactsSession))
        {
            try
            {
                var conversionResult = await ConvertResultsSafelyAsync(
                        artifactsSession,
                        context.AllowEmptyTestRun,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (conversionResult is UnityResultsConversionSuccess success)
                {
                    conversionSuccess = success;
                }
                else
                {
                    primaryFailure = CreateConversionFailure(
                        (UnityResultsConversionFailure)conversionResult);
                }
            }
            catch (Exception exception)
            {
                primaryFailure = ApplicationFailure.InternalError(
                    $"Unexpected error during Unity results conversion: {exception.Message}",
                    UcliCoreErrorCodes.InternalError,
                    instancePath: null,
                    startupFailure: null);
            }
        }
        else
        {
            primaryFailure = CreateUnityExecutionFailure(executionFailure);
        }

        // NOTE:
        // Completion metadata must be written even when caller cancellation is requested,
        // so mapping can preserve run-scoped diagnostics.
        var completionResult = await CompleteArtifactsSafelyAsync(
            configuration,
            artifactsSession,
            context.Target,
            CancellationToken.None).ConfigureAwait(false);
        var finalizationFailure = completionResult.IsSuccess
            ? null
            : ApplicationFailure.FromExecutionError(completionResult.Error!);
        if (primaryFailure is not null)
        {
            return finalizationFailure is null
                ? TestRunExecutionPipelineResult.FailedAfterArtifacts(
                    artifactsSession,
                    primaryFailure)
                : TestRunExecutionPipelineResult.FailedAfterArtifactsWithFinalizationFailure(
                    artifactsSession,
                    primaryFailure,
                    finalizationFailure);
        }

        if (finalizationFailure is not null)
        {
            return TestRunExecutionPipelineResult.FailedAfterArtifacts(
                artifactsSession,
                finalizationFailure);
        }

        return TestRunExecutionPipelineResult.Completed(
            artifactsSession,
            conversionSuccess
                ?? throw new InvalidOperationException(
                    "A completed Test Run pipeline must contain normalized result evidence."));
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
                $"Unexpected error during artifacts preparation: {exception.Message}", UcliCoreErrorCodes.InternalError));
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
                $"Unexpected error during artifacts completion: {exception.Message}", UcliCoreErrorCodes.InternalError));
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
            return UnityTestExecutionResult.Canceled(
                "Unity test execution was canceled.",
                ExecutionErrorCodes.Canceled);
        }
        catch (TestRunProgressProtocolException exception)
        {
            return UnityTestExecutionResult.ProgressProtocolViolation(
                exception.Message,
                TestRunErrorCodes.UnityTestExecutionFailed);
        }
        catch (Exception exception)
        {
            return UnityTestExecutionResult.InternalError(
                $"Unexpected error during Unity test execution: {exception.Message}",
                UcliCoreErrorCodes.InternalError);
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
            testPlatform: configuration.TestPlatform,
            testFilter: configuration.TestFilter,
            testCategories: configuration.TestCategories,
            assemblyNames: configuration.AssemblyNames,
            failFast: context.FailFast,
            runId: session.RunId);
    }

    private static async ValueTask ForwardTestRunProgressFrameAsync (
        UnityRequestProgressFrame frame,
        Guid expectedRunId,
        ICommandProgressSink progressSink,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(frame);
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
        Guid expectedRunId,
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

        var response = requestResult.Response!;
        if (response.Errors.Count != 0)
        {
            var firstError = response.Errors[0];
            return CreateReportedFailure(
                $"Unity test run failed with error code '{firstError.Code}'. {firstError.Message}",
                firstError.Code,
                target);
        }

        if (!TryReadExitCode(response.Payload, out var exitCode, out var readError))
        {
            return UnityTestExecutionResult.InvalidResponse(
                $"Unity test run payload is invalid. {readError}",
                UcliCoreErrorCodes.InternalError);
        }

        var processExitResult = UnityTestExecutionResult.FromProcessExitCode(exitCode);
        if (processExitResult is UnityTestExecutionResult.ExecutionFailure)
        {
            return processExitResult;
        }

        var artifactsExistenceResult = artifactExistenceProbe.ValidateGeneratedFiles(session.Paths);
        if (!artifactsExistenceResult.IsSuccess)
        {
            return UnityTestExecutionResult.ArtifactMissing(
                artifactsExistenceResult.ErrorMessage!,
                TestRunErrorCodes.UnityTestExecutionFailed);
        }

        return processExitResult;
    }

    private bool CanRecoverCompletedOneshotResults (
        UnityTestExecutionResult.ExecutionFailure executionFailure,
        UnityExecutionTarget target,
        ArtifactsSession session)
    {
        if (target != UnityExecutionTarget.Oneshot
            || executionFailure.FailureKind != UnityTestExecutionFailureKind.IpcTransportInterrupted)
        {
            return false;
        }

        // NOTE:
        // Unity Test Runner can close oneshot IPC during post-test domain reload after it has
        // already written complete results. Treat only a classified transport interruption as recoverable;
        // all other abnormal exits preserve the primary execution failure.
        return artifactExistenceProbe.ValidateGeneratedFiles(session.Paths).IsSuccess;
    }

    private static UnityTestExecutionResult CreateRequestFailure (
        UnityRequestFailure failure,
        UnityExecutionTarget target)
    {
        if (failure.FailureKind == UnityRequestFailureKind.TransportInterrupted)
        {
            return UnityTestExecutionResult.IpcTransportInterrupted(
                failure.Message,
                failure.Code);
        }

        if (failure.Code == ExecutionErrorCodes.IpcTimeout)
        {
            return target == UnityExecutionTarget.Oneshot
                ? UnityTestExecutionResult.ProcessTimedOut(failure.Message, failure.Code)
                : UnityTestExecutionResult.IpcTimedOut(failure.Message, failure.Code);
        }

        if (failure.Code == ExecutionErrorCodes.Canceled)
        {
            return UnityTestExecutionResult.Canceled(failure.Message, failure.Code);
        }

        if (failure.Code == UnityExecutionModeDecisionErrorCodes.DaemonNotRunning)
        {
            return UnityTestExecutionResult.StartFailed(
                failure.Message,
                failure.Code,
                failure.StartupFailure);
        }

        return failure.Code == UcliCoreErrorCodes.InternalError
            ? UnityTestExecutionResult.InternalError(failure.Message, failure.Code)
            : UnityTestExecutionResult.RequestFailed(failure.Message, failure.Code);
    }

    private static UnityTestExecutionResult CreateReportedFailure (
        string errorMessage,
        UcliCode errorCode,
        UnityExecutionTarget target)
    {
        if (errorCode == IpcTransportErrorCodes.IpcTimeout
            || errorCode == ExecutionErrorCodes.IpcTimeout)
        {
            return target == UnityExecutionTarget.Oneshot
                ? UnityTestExecutionResult.ProcessTimedOut(errorMessage, errorCode)
                : UnityTestExecutionResult.IpcTimedOut(errorMessage, errorCode);
        }

        if (errorCode == ExecutionErrorCodes.Canceled)
        {
            return UnityTestExecutionResult.Canceled(errorMessage, errorCode);
        }

        return InvalidArgumentErrorCodeSet.Contains(errorCode)
            ? UnityTestExecutionResult.InvalidArgument(errorMessage, errorCode)
            : UnityTestExecutionResult.RequestFailed(errorMessage, errorCode);
    }

    private static ApplicationFailure CreateUnityExecutionFailure (
        UnityTestExecutionResult.ExecutionFailure failure)
    {
        if (failure.FailureKind == UnityTestExecutionFailureKind.InvalidArgument)
        {
            return ApplicationFailure.InvalidInput(
                failure.ErrorMessage,
                failure.ErrorCode,
                instancePath: null,
                startupFailure: failure.StartupFailure);
        }

        if (failure.FailureKind == UnityTestExecutionFailureKind.Canceled)
        {
            return ApplicationFailure.Create(
                ApplicationFailureKind.Canceled,
                failure.ErrorMessage,
                failure.ErrorCode,
                instancePath: null,
                outcome: ApplicationOutcome.ToolError,
                startupFailure: failure.StartupFailure);
        }

        return IsUnityExecutionInfrastructureFailure(failure.FailureKind)
            ? ApplicationFailure.Create(
                ApplicationFailureKind.ExternalProcessFailure,
                failure.ErrorMessage,
                failure.ErrorCode,
                instancePath: null,
                outcome: ApplicationOutcome.InfrastructureError,
                startupFailure: failure.StartupFailure)
            : ApplicationFailure.Create(
                ApplicationFailureKind.ExternalProcessFailure,
                failure.ErrorMessage,
                failure.ErrorCode,
                instancePath: null,
                outcome: ApplicationOutcome.ToolError,
                startupFailure: failure.StartupFailure);
    }

    private static bool IsUnityExecutionInfrastructureFailure (
        UnityTestExecutionFailureKind failureKind)
    {
        return failureKind switch
        {
            UnityTestExecutionFailureKind.IpcTimedOut
                or UnityTestExecutionFailureKind.ProcessTimedOut
                or UnityTestExecutionFailureKind.IpcTransportInterrupted
                or UnityTestExecutionFailureKind.AbnormalExit
                or UnityTestExecutionFailureKind.ArtifactMissing
                or UnityTestExecutionFailureKind.RequestFailed
                or UnityTestExecutionFailureKind.InvalidResponse
                or UnityTestExecutionFailureKind.InternalError => true,
            UnityTestExecutionFailureKind.StartFailed
                or UnityTestExecutionFailureKind.Canceled
                or UnityTestExecutionFailureKind.InvalidArgument
                or UnityTestExecutionFailureKind.ProgressProtocolViolation => false,
            _ => throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Unity test execution failure kind must be a defined value."),
        };
    }

    private static ApplicationFailure CreateConversionFailure (
        UnityResultsConversionFailure failure)
    {
        return failure.FailureKind switch
        {
            UnityResultsConversionFailureKind.OutputWriteFailed =>
                ApplicationFailure.Create(
                    ApplicationFailureKind.ExternalProcessFailure,
                    failure.ErrorMessage,
                    TestRunErrorCodes.TestResultsOutputWriteFailed,
                    instancePath: null,
                    outcome: ApplicationOutcome.InfrastructureError,
                    startupFailure: null),
            UnityResultsConversionFailureKind.ResultsXmlReadFailed =>
                ApplicationFailure.Create(
                    ApplicationFailureKind.ExternalProcessFailure,
                    failure.ErrorMessage,
                    TestRunErrorCodes.TestResultsXmlReadFailed,
                    instancePath: null,
                    outcome: ApplicationOutcome.InfrastructureError,
                    startupFailure: null),
            UnityResultsConversionFailureKind.Canceled =>
                ApplicationFailure.Create(
                    ApplicationFailureKind.Canceled,
                    failure.ErrorMessage,
                    ExecutionErrorCodes.Canceled,
                    instancePath: null,
                    outcome: ApplicationOutcome.ToolError,
                    startupFailure: null),
            UnityResultsConversionFailureKind.InvalidResultsXml =>
                ApplicationFailure.Create(
                    ApplicationFailureKind.ExternalProcessFailure,
                    failure.ErrorMessage,
                    TestRunErrorCodes.TestResultsXmlInvalid,
                    instancePath: null,
                    outcome: ApplicationOutcome.ToolError,
                    startupFailure: null),
            _ => throw new ArgumentOutOfRangeException(
                nameof(failure),
                failure.FailureKind,
                "Unity results conversion failure kind must be a defined value."),
        };
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
                $"Unexpected error during test-run progress emission: {exception.Message}", UcliCoreErrorCodes.InternalError));
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
    /// <param name="allowEmptyTestRun"> Whether a run containing no test cases satisfies the requested test condition. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to results conversion result. </returns>
    private async ValueTask<UnityResultsConversionResult> ConvertResultsSafelyAsync (
        ArtifactsSession session,
        bool allowEmptyTestRun,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await resultsConverter
                .ConvertAsync(session, allowEmptyTestRun, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UnityResultsConversionResult.Canceled(
                "Unity results conversion was canceled.");
        }
    }
}
