using System.Globalization;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayStatusServiceIpcStatusTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenGuiSessionAndIpcSucceeds_ReturnsFlatStatusProjection ()
    {
        var context = PlayProjectContext;
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(CreatePlaySession()));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse())));
        var service = CreateService(context, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput("/repo/UnityProject", 1500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(context.UnityProject.UnityProjectRoot, output.Project.ProjectPath);
        Assert.Equal(context.UnityProject.ProjectFingerprint, output.Project.ProjectFingerprint);
        Assert.Equal("6000.1.4f1", output.Project.UnityVersion);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal("gui", output.EditorMode);
        Assert.Equal(IpcEditorLifecycleStateCodec.Ready, output.LifecycleState);
        Assert.Null(output.BlockingReason);
        Assert.Equal(IpcCompileStateCodec.Ready, output.CompileState);
        Assert.Equal("12", output.CompileGeneration);
        Assert.Equal("7", output.DomainReloadGeneration);
        Assert.True(output.CanAcceptExecutionRequests);
        Assert.Equal("2026-05-21T00:00:00.0000000+00:00", output.ObservedAtUtc?.ToString("O", CultureInfo.InvariantCulture));
        Assert.Null(output.ActionRequired);
        Assert.Null(output.PrimaryDiagnostic);
        Assert.Equal("stopped", output.PlayMode.State);
        Assert.Equal("none", output.PlayMode.Transition);
        Assert.False(output.PlayMode.IsPlaying);
        Assert.False(output.PlayMode.IsPlayingOrWillChangePlaymode);
        Assert.Equal("2", output.PlayMode.Generation);
        Assert.Equal(1500, output.TimeoutMilliseconds);

        UnityRequestExecutorInvocationAssert.PlayStatusOnce(
            requestExecutor,
            TimeSpan.FromMilliseconds(1500));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlayModeIsPlaying_ReturnsPlayingSnapshot ()
    {
        var playMode = new IpcPlayModeSnapshot(
            State: "playing",
            Transition: "none",
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true,
            Generation: "9");
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(CreatePlaySession()));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateStatusResponse(playMode: playMode))));
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayStatusExecutionOutput>(result.Output);
        Assert.Equal("playing", output.PlayMode.State);
        Assert.Equal("none", output.PlayMode.Transition);
        Assert.True(output.PlayMode.IsPlaying);
        Assert.True(output.PlayMode.IsPlayingOrWillChangePlaymode);
        Assert.Equal("9", output.PlayMode.Generation);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcExecutionTimesOut_ReturnsTimeoutError ()
    {
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(CreatePlaySession()));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            UnityRequestFailureKind.General,
            ExecutionErrorCodes.IpcTimeout,
            "play status timed out")));
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcExecutionFailsWithoutTimeout_PreservesFailureCodeAndMessage ()
    {
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(CreatePlaySession()));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            UnityRequestFailureKind.General,
            UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
            "Daemon is not running.")));
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, error.Code);
        Assert.Equal("Daemon is not running.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcErrorResponseIsReturned_PreservesErrorCode ()
    {
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(CreatePlaySession()));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponse(
            UcliCoreErrorCodes.InvalidArgument,
            "PlayStatus payload is invalid.")));
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayStatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Equal("PlayStatus payload is invalid.", error.Message);
    }
}
