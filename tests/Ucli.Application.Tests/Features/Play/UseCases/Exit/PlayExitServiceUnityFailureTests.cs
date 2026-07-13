using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayExitServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayExitServiceUnityFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsTransitionTimeout_ReturnsFailureWithObservedPayload ()
    {
        var before = CreateSnapshot("playmode", "playMode", false, CreatePlayingPlayMode("2"));
        var observed = CreateSnapshot("playmode", "playMode", false, new IpcPlayModeSnapshot(
            State: "exiting",
            Transition: "exiting",
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true,
            Generation: "2"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Timeout,
            before)
        {
            Observed = observed,
            ApplicationState = IpcPlayApplicationStateNames.Indeterminate,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponse(
            response,
            PlayModeErrorCodes.PlayModeTransitionTimeout,
            "Unity Play Mode exit timed out after 1500 milliseconds.")));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, 1500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeTransitionTimeout, result.Error!.Code);
        Assert.NotNull(result.Output);
        Assert.Equal(IpcPlayTransitionResultNames.Timeout, result.Output!.Transition.Result);
        Assert.Equal(IpcPlayApplicationStateNames.Indeterminate, result.Output.Transition.ApplicationState);
        Assert.Equal(observed.PlayMode!.State, result.Output.Transition.Observed!.PlayMode!.State);
        Assert.Null(result.Output.Transition.After);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsAppliedBlockedTransition_ReturnsFailureWithoutAfter ()
    {
        var before = CreateSnapshot("playmode", "playMode", false, CreatePlayingPlayMode("2"));
        var observed = CreateSnapshot("safeMode", "safeMode", false, CreateStoppedPlayMode("3"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Blocked,
            before)
        {
            Observed = observed,
            ApplicationState = IpcPlayApplicationStateNames.Applied,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponse(
            response,
            PlayModeErrorCodes.PlayModeTransitionBlocked,
            "Unity Play Mode exit completed but readiness was blocked.")));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeTransitionBlocked, result.Error!.Code);
        Assert.NotNull(result.Output);
        Assert.Equal(IpcPlayApplicationStateNames.Applied, result.Output!.Transition.ApplicationState);
        Assert.Null(result.Output.Transition.After);
        Assert.Equal("safeMode", result.Output.LifecycleState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityErrorOmitsTransitionPayload_ReturnsOriginalError ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponseWithoutTransitionPayload(
            UcliCoreErrorCodes.InvalidArgument,
            "Unity play exit payload is invalid.")));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error!.Code);
        Assert.Equal("Unity play exit payload is invalid.", result.Error.Message);
    }
}
