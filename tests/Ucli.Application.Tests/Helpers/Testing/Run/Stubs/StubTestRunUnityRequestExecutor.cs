using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubTestRunUnityRequestExecutor : IUnityRequestExecutor, IUnityStreamingRequestExecutor
{
    public ValueTask<LifecycleExecutionHostBindingResolution> BindAsync (UnityExecutionMode mode, ResolvedUnityProjectContext project, ExecutionDeadline executionDeadline, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Lifecycle execution host binding is not supported by this test stub.");
    }
    public ValueTask<LifecycleExecutionHostBindingResolution> BindReconnectAsync (ResolvedUnityProjectContext project, LifecycleExecutionStartBinding requiredStart, ExecutionDeadline callerWaitDeadline, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Lifecycle execution reconnect binding is not supported by this test stub.");
    }
    public ValueTask<LifecycleExecutionHostBindingResolution> BindResolvedTargetAsync (ResolvedUnityProjectContext project, UnityExecutionTarget target, ExecutionDeadline executionDeadline, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Resolved Lifecycle execution host binding is not supported by this test stub.");
    }
    private readonly StubUnityTestExecutor unityTestExecutor;
    private readonly RecordingDaemonTestRunClient? daemonTestRunClient;
    private readonly IReadOnlyList<UnityRequestProgressFrame>? streamingProgressFrames;
    private readonly UnityRequestResponse? responseOverride;

    private readonly Func<Guid, ArtifactPaths> artifactPathsResolver;

    public StubTestRunUnityRequestExecutor (
        StubUnityTestExecutor unityTestExecutor,
        RecordingDaemonTestRunClient? daemonTestRunClient,
        IReadOnlyList<UnityRequestProgressFrame>? streamingProgressFrames,
        UnityRequestResponse? responseOverride,
        Func<Guid, ArtifactPaths> artifactPathsResolver)
    {
        this.unityTestExecutor = unityTestExecutor;
        this.daemonTestRunClient = daemonTestRunClient;
        this.streamingProgressFrames = streamingProgressFrames;
        this.responseOverride = responseOverride;
        this.artifactPathsResolver = artifactPathsResolver ?? throw new ArgumentNullException(nameof(artifactPathsResolver));
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
        var artifactPaths = artifactPathsResolver(testRunRequest.RunId);
        var configuration = TestRunServiceTestFactory.CreateResolvedConfiguration();
        if (daemonTestRunClient is not null)
        {
            var requestResult = await daemonTestRunClient
                .ExecuteAsync(configuration, artifactPaths, timeout, testRunRequest.FailFast, cancellationToken)
                .ConfigureAwait(false);
            if (requestResult.IsSuccess)
            {
                await WriteProgressFramesAsync(
                        testRunRequest.RunId,
                        onProgressFrame,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return requestResult;
        }

        var executionResult = await unityTestExecutor
            .ExecuteAsync(configuration, artifactPaths, timeout, cancellationToken)
            .ConfigureAwait(false);
        if (executionResult is UnityTestExecutionResult.ObservedProcessCompletion completed)
        {
            EnsureArtifactFiles(artifactPaths);
            await WriteProgressFramesAsync(
                    testRunRequest.RunId,
                    onProgressFrame,
                    cancellationToken)
                .ConfigureAwait(false);

            return UnityRequestExecutionResult.Success(new UnityRequestResponse(
                IpcPayloadCodec.SerializeToElement(new IpcTestRunResponse(completed.ProcessExitCode)),
                Array.Empty<OperationExecutionError>()));
        }

        return UnityRequestExecutionResult.Failure(
            CreateUnityRequestFailure(
                Assert.IsAssignableFrom<UnityTestExecutionResult.ExecutionFailure>(executionResult)));
    }

    private async ValueTask WriteProgressFramesAsync (
        Guid runId,
        Func<UnityRequestProgressFrame, CancellationToken, ValueTask>? onProgressFrame,
        CancellationToken cancellationToken)
    {
        if (onProgressFrame is null)
        {
            return;
        }

        var progressFrames = streamingProgressFrames ?? [
            new UnityRequestProgressFrame(
                TestRunProgressEventNames.RunDiagnostic,
                IpcPayloadCodec.SerializeToElement(new TestRunDiagnosticEntry(
                    runId,
                    new UcliCode("TEST_PROGRESS_STUB"),
                    "stub progress",
                    UcliDiagnosticSeverity.Info))),
        ];
        foreach (var progressFrame in progressFrames)
        {
            await onProgressFrame(progressFrame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static UnityRequestPayload.TestRun ReadTestRunRequest (UnityRequestPayload payload)
    {
        return Assert.IsType<UnityRequestPayload.TestRun>(payload);
    }

    private static void EnsureArtifactFiles (ArtifactPaths artifactPaths)
    {
        Directory.CreateDirectory(artifactPaths.ArtifactsDir.Value);
        File.WriteAllText(artifactPaths.ResultsXmlPath.Value, "<test-run />");
        File.WriteAllText(artifactPaths.EditorLogPath.Value, string.Empty);
    }

    private static UnityRequestFailure CreateUnityRequestFailure (
        UnityTestExecutionResult.ExecutionFailure failure)
    {
        return failure.FailureKind switch
        {
            UnityTestExecutionFailureKind.IpcTransportInterrupted =>
                new UnityRequestFailure(
                    UnityRequestFailureKind.TransportInterrupted,
                    failure.ErrorCode,
                    failure.ErrorMessage),
            UnityTestExecutionFailureKind.IpcTimedOut
                or UnityTestExecutionFailureKind.ProcessTimedOut =>
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    failure.ErrorCode,
                    failure.ErrorMessage,
                    failure.StartupFailure),
            UnityTestExecutionFailureKind.Canceled =>
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    failure.ErrorCode,
                    failure.ErrorMessage),
            UnityTestExecutionFailureKind.InternalError =>
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    failure.ErrorCode,
                    failure.ErrorMessage),
            UnityTestExecutionFailureKind.StartFailed
                or UnityTestExecutionFailureKind.InvalidArgument
                or UnityTestExecutionFailureKind.AbnormalExit
                or UnityTestExecutionFailureKind.ArtifactMissing
                or UnityTestExecutionFailureKind.ProgressProtocolViolation
                or UnityTestExecutionFailureKind.RequestFailed
                or UnityTestExecutionFailureKind.InvalidResponse =>
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    failure.ErrorCode,
                    failure.ErrorMessage,
                    failure.StartupFailure),
            _ => throw new ArgumentOutOfRangeException(
                nameof(failure),
                failure.FailureKind,
                "Unity test execution failure kind must be defined."),
        };
    }
}
