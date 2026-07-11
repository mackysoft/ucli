using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubTestRunUnityRequestExecutor : IUnityRequestExecutor, IUnityStreamingRequestExecutor
{
    private readonly StubUnityTestExecutor unityTestExecutor;
    private readonly RecordingDaemonTestRunClient? daemonTestRunClient;
    private readonly IReadOnlyList<UnityRequestProgressFrame>? streamingProgressFrames;
    private readonly UnityRequestResponse? responseOverride;

    public StubTestRunUnityRequestExecutor (
        StubUnityTestExecutor unityTestExecutor,
        RecordingDaemonTestRunClient? daemonTestRunClient,
        IReadOnlyList<UnityRequestProgressFrame>? streamingProgressFrames,
        UnityRequestResponse? responseOverride)
    {
        this.unityTestExecutor = unityTestExecutor;
        this.daemonTestRunClient = daemonTestRunClient;
        this.streamingProgressFrames = streamingProgressFrames;
        this.responseOverride = responseOverride;
    }

    public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(timeout, payload, onProgressFrame: null, cancellationToken);
    }

    public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        Func<UnityRequestProgressFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(timeout, payload, onProgressFrame, cancellationToken);
    }

    private async ValueTask<UnityRequestExecutionResult> ExecuteCoreAsync (
        TimeSpan timeout,
        UnityRequestPayload payload,
        Func<UnityRequestProgressFrame, CancellationToken, ValueTask>? onProgressFrame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (responseOverride is not null)
        {
            return UnityRequestExecutionResult.Success(responseOverride);
        }

        var testRunRequest = ReadTestRunRequest(payload);
        var artifactPaths = CreateArtifactPaths(testRunRequest);
        var configuration = TestRunServiceTestFactory.CreateResolvedConfiguration();
        var executionResult = daemonTestRunClient is null
            ? await unityTestExecutor.ExecuteAsync(configuration, artifactPaths, timeout, cancellationToken)
                .ConfigureAwait(false)
            : await daemonTestRunClient.ExecuteAsync(configuration, artifactPaths, timeout, testRunRequest.FailFast, cancellationToken)
                .ConfigureAwait(false);

        if (executionResult.IsSuccess)
        {
            EnsureArtifactFiles(artifactPaths);
            if (onProgressFrame is not null)
            {
                var progressFrames = streamingProgressFrames ?? [
                    new UnityRequestProgressFrame(
                        TestRunProgressEventNames.RunDiagnostic,
                        IpcPayloadCodec.SerializeToElement(new TestRunDiagnosticEntry(
                            testRunRequest.RunId ?? "run-id",
                            "TEST_PROGRESS_STUB",
                            "stub progress",
                            "info"))),
                ];
                foreach (var progressFrame in progressFrames)
                {
                    await onProgressFrame(progressFrame, cancellationToken).ConfigureAwait(false);
                }
            }

            return UnityRequestExecutionResult.Success(new UnityRequestResponse(
                IpcPayloadCodec.SerializeToElement(new IpcTestRunResponse(executionResult.ProcessExitCode!.Value)),
                Array.Empty<OperationExecutionError>(),
                HasFailureStatus: false));
        }

        return UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            ResolveFailureKind(executionResult),
            ResolveErrorCode(executionResult),
            executionResult.ErrorMessage ?? "Unity test execution failed.",
            executionResult.StartupFailure));
    }

    private static UnityRequestPayload.TestRun ReadTestRunRequest (UnityRequestPayload payload)
    {
        return Assert.IsType<UnityRequestPayload.TestRun>(payload);
    }

    private static ArtifactPaths CreateArtifactPaths (UnityRequestPayload.TestRun request)
    {
        var artifactsDir = Path.GetDirectoryName(request.ResultsXmlPath) ?? Path.GetTempPath();
        return new ArtifactPaths(
            ArtifactsDir: artifactsDir,
            MetaJsonPath: Path.Combine(artifactsDir, "meta.json"),
            ResultsXmlPath: request.ResultsXmlPath,
            EditorLogPath: request.EditorLogPath,
            ResultsJsonPath: Path.Combine(artifactsDir, "results.json"),
            SummaryJsonPath: Path.Combine(artifactsDir, "summary.json"));
    }

    private static void EnsureArtifactFiles (ArtifactPaths artifactPaths)
    {
        Directory.CreateDirectory(artifactPaths.ArtifactsDir);
        File.WriteAllText(artifactPaths.ResultsXmlPath, "<test-run />");
        File.WriteAllText(artifactPaths.EditorLogPath, string.Empty);
    }

    private static UnityRequestFailureKind ResolveFailureKind (UnityTestExecutionResult executionResult)
    {
        return executionResult.FailureKind == UnityTestExecutionFailureKind.IpcTransportInterrupted
            ? UnityRequestFailureKind.TransportInterrupted
            : UnityRequestFailureKind.General;
    }

    private static UcliCode ResolveErrorCode (UnityTestExecutionResult executionResult)
    {
        if (executionResult.ErrorCode is { IsValid: true } code)
        {
            return code;
        }

        return executionResult.FailureKind switch
        {
            UnityTestExecutionFailureKind.IpcTimedOut => ExecutionErrorCodes.IpcTimeout,
            UnityTestExecutionFailureKind.ProcessTimedOut => ExecutionErrorCodes.IpcTimeout,
            UnityTestExecutionFailureKind.Canceled => ExecutionErrorCodes.Canceled,
            UnityTestExecutionFailureKind.ArtifactMissing => TestRunErrorCodes.UnityTestExecutionFailed,
            _ => UcliCoreErrorCodes.InternalError,
        };
    }
}
