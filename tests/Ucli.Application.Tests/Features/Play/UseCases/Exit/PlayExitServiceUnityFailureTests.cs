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
        var before = CreateSnapshot(
            IpcEditorLifecycleState.PlayMode,
            CreatePlayingPlayMode(),
            playModeGeneration: 2);
        var observed = CreateSnapshot(IpcEditorLifecycleState.PlayMode, new IpcPlayModeSnapshot(
            State: IpcPlayModeState.Exiting,
            Transition: IpcPlayModeTransition.Exiting,
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true),
            playModeGeneration: 2);
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommand.Exit,
            IpcPlayTransitionOutcome.Timeout,
            before,
            After: null,
            Observed: observed,
            ApplicationState: IpcApplicationState.Indeterminate));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponse(
            response,
            PlayModeErrorCodes.PlayModeTransitionTimeout,
            "Unity Play Mode exit timed out after 1500 milliseconds.")));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, 1500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeTransitionTimeout, result.Error!.Code);
        Assert.NotNull(result.Output);
        Assert.Equal(IpcPlayTransitionOutcome.Timeout, result.Output!.Transition.Result);
        Assert.Equal(IpcApplicationState.Indeterminate, result.Output.Transition.ApplicationState);
        Assert.Equal(observed.State.PlayMode.State, result.Output.Transition.Observed!.PlayMode.State);
        Assert.Null(result.Output.Transition.After);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsAppliedBlockedTransition_ReturnsFailureWithoutAfter ()
    {
        var before = CreateSnapshot(
            IpcEditorLifecycleState.PlayMode,
            CreatePlayingPlayMode(),
            playModeGeneration: 2);
        var observed = CreateSnapshot(
            IpcEditorLifecycleState.SafeMode,
            CreateStoppedPlayMode(),
            playModeGeneration: 3);
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommand.Exit,
            IpcPlayTransitionOutcome.Blocked,
            before,
            After: null,
            Observed: observed,
            ApplicationState: IpcApplicationState.Applied));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponse(
            response,
            PlayModeErrorCodes.PlayModeTransitionBlocked,
            "Unity Play Mode exit completed but readiness was blocked.")));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeTransitionBlocked, result.Error!.Code);
        Assert.NotNull(result.Output);
        Assert.Equal(IpcApplicationState.Applied, result.Output!.Transition.ApplicationState);
        Assert.Null(result.Output.Transition.After);
        Assert.Equal(IpcEditorLifecycleState.SafeMode, result.Output.LifecycleState);
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
