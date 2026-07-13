using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayStatusServiceSidecarTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcExecutionTimesOutAndFreshLifecycleSidecarExists_ReturnsSidecarStatus ()
    {
        var session = CreatePlaySession();
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(session));
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(CreateLifecycleObservation(
                session,
                IpcEditorLifecycleState.Ready,
                playModeState: "stopped",
                isPlaying: false,
                isPlayingOrWillChangePlaymode: false)),
        };
        var processIdentityAssessor = CreateProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
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
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal("ready", output.LifecycleState);
        Assert.Null(output.BlockingReason);
        Assert.True(output.CanAcceptExecutionRequests);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal("stopped", output.PlayMode.State);
        Assert.Equal("none", output.PlayMode.Transition);
        Assert.False(output.PlayMode.IsPlaying);
        UnityRequestExecutorInvocationAssert.PlayStatusOnce(requestExecutor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcExecutionTimesOutAndLifecycleSidecarLacksEditorInstanceId_ReturnsTimeoutError ()
    {
        var session = CreatePlaySession();
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(session));
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                CreateLifecycleObservation(session) with
                {
                    EditorInstanceId = null,
                }),
        };
        var processIdentityAssessor = CreateProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
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
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(session));
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
        Assert.Equal("playmode", output.LifecycleState);
        Assert.Equal("playing", output.PlayMode.State);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenLifecycleSidecarDoesNotMatchLiveProcess_ReturnsIpcStatus ()
    {
        var session = CreatePlaySession();
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(session));
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
        Assert.Equal("ready", output.LifecycleState);
        Assert.Equal("stopped", output.PlayMode.State);
        Assert.Equal("2", output.PlayMode.Generation);
        UnityRequestExecutorInvocationAssert.PlayStatusOnce(requestExecutor);
    }
}
