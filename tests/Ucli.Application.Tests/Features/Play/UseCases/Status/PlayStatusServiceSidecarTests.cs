using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayStatusServiceSidecarTests
{
    private static readonly Guid OtherEditorInstanceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcExecutionTimesOutAndFreshLifecycleSidecarReportsReady_ReturnsTimeoutError ()
    {
        var session = CreatePlaySession();
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(session));
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(CreateLifecycleObservation(
                session,
                IpcEditorLifecycleState.Ready,
                playModeState: IpcPlayModeState.Stopped,
                isPlaying: false,
                isPlayingOrWillChangePlaymode: false)),
        };
        var processIdentityAssessor = CreateProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            UnityRequestFailureKind.General,
            ExecutionErrorCodes.IpcTimeout,
            "play status timed out")));
        var service = CreateService(
            PlayProjectContext,
            sessionStore,
            requestExecutor,
            lifecycleStore,
            processIdentityAssessor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        DaemonLifecycleObservationAssert.LifecycleObservationReadTwiceFor(
            lifecycleStore,
            PlayProjectContext,
            CancellationToken.None);
        UnityRequestExecutorInvocationAssert.PlayStatusOnce(requestExecutor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcTimesOutAfterSidecarChangesFromReadyToBusy_ReturnsLatestBusyStatus ()
    {
        var session = CreatePlaySession();
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(session));
        var readyObservation = CreateLifecycleObservation(
            session,
            IpcEditorLifecycleState.Ready,
            playModeState: IpcPlayModeState.Stopped,
            isPlaying: false,
            isPlayingOrWillChangePlaymode: false);
        var busyObservation = CreateLifecycleObservation(
            session,
            IpcEditorLifecycleState.Busy,
            playModeState: IpcPlayModeState.Stopped,
            isPlaying: false,
            isPlayingOrWillChangePlaymode: false);
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        lifecycleStore.ReadAsyncHandler = (_, _, _) => ValueTask.FromResult(
            DaemonLifecycleObservationReadResult.Success(
                lifecycleStore.ReadInvocations.Count == 1
                    ? readyObservation
                    : busyObservation));
        var processIdentityAssessor = CreateProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            UnityRequestFailureKind.General,
            ExecutionErrorCodes.IpcTimeout,
            "play status timed out")));
        var service = CreateService(
            PlayProjectContext,
            sessionStore,
            requestExecutor,
            lifecycleStore,
            processIdentityAssessor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayStatusExecutionOutput>(result.Output);
        Assert.Equal(IpcEditorLifecycleState.Busy, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReason.Busy, output.BlockingReason);
        Assert.False(output.CanAcceptExecutionRequests);
        DaemonLifecycleObservationAssert.LifecycleObservationReadTwiceFor(
            lifecycleStore,
            PlayProjectContext,
            CancellationToken.None);
        UnityRequestExecutorInvocationAssert.PlayStatusOnce(requestExecutor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcExecutionTimesOutAndLifecycleSidecarHasDifferentEditorInstanceId_ReturnsTimeoutError ()
    {
        var session = CreatePlaySession();
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(session));
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                CreateLifecycleObservation(session, editorInstanceId: OtherEditorInstanceId)),
        };
        var processIdentityAssessor = CreateProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            UnityRequestFailureKind.General,
            ExecutionErrorCodes.IpcTimeout,
            "play status timed out")));
        var service = CreateService(
            PlayProjectContext,
            sessionStore,
            requestExecutor,
            lifecycleStore,
            processIdentityAssessor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        DaemonLifecycleObservationAssert.LifecycleObservationReadTwiceFor(lifecycleStore, PlayProjectContext, CancellationToken.None);
        UnityRequestExecutorInvocationAssert.PlayStatusOnce(requestExecutor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenFreshLifecycleSidecarReportsPlayMode_ReturnsWithoutIpcCall ()
    {
        var session = CreatePlaySession();
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(session));
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(CreateLifecycleObservation(session)),
        };
        var processIdentityAssessor = CreateProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var requestExecutor = new UnexpectedUnityRequestExecutor();
        var service = CreateService(
            PlayProjectContext,
            sessionStore,
            requestExecutor,
            lifecycleStore,
            processIdentityAssessor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayStatusExecutionOutput>(result.Output);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal(IpcEditorLifecycleState.PlayMode, output.LifecycleState);
        Assert.Equal(IpcPlayModeState.Playing, output.PlayMode.State);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenLifecycleSidecarDoesNotMatchLiveProcess_ReturnsIpcStatus ()
    {
        var session = CreatePlaySession();
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(session));
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(CreateLifecycleObservation(session)),
        };
        var processIdentityAssessor = CreateProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.DifferentProcess);
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse())));
        var service = CreateService(
            PlayProjectContext,
            sessionStore,
            requestExecutor,
            lifecycleStore,
            processIdentityAssessor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayStatusExecutionOutput>(result.Output);
        Assert.Equal(IpcEditorLifecycleState.Ready, output.LifecycleState);
        Assert.Equal(IpcPlayModeState.Stopped, output.PlayMode.State);
        Assert.Equal(2, output.Generations!.PlayModeGeneration);
        UnityRequestExecutorInvocationAssert.PlayStatusOnce(requestExecutor);
    }
}
