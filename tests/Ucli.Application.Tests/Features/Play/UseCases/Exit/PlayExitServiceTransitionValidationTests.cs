using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayExitServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayExitServiceTransitionValidationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAlreadyExitedChangesGeneration_ReturnsStateUnknown ()
    {
        var before = CreateSnapshot(
            UnityEditorLifecycleState.Compiling,
            CreateStoppedPlayMode(),
            playModeGeneration: 9);
        var after = CreateSnapshot(
            UnityEditorLifecycleState.Compiling,
            CreateStoppedPlayMode(),
            playModeGeneration: 10);
        var response = new IpcPlayTransitionResponse(CreateTerminalReference(), new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Exit,
            PlayLifecycleTransitionOutcome.AlreadyExited,
            before,
            After: after,
            Observed: null,
            ApplicationState: null));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExitedDoesNotChangeGeneration_ReturnsStateUnknown ()
    {
        var before = CreateSnapshot(
            UnityEditorLifecycleState.PlayMode,
            CreatePlayingPlayMode(),
            playModeGeneration: 2);
        var after = CreateSnapshot(
            UnityEditorLifecycleState.Ready,
            CreateStoppedPlayMode(),
            playModeGeneration: 2);
        var response = new IpcPlayTransitionResponse(CreateTerminalReference(), new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Exit,
            PlayLifecycleTransitionOutcome.Exited,
            before,
            After: after,
            Observed: null,
            ApplicationState: null));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExitedAfterSnapshotIsStillPlaymode_ReturnsStateUnknown ()
    {
        var before = CreateSnapshot(
            UnityEditorLifecycleState.PlayMode,
            CreatePlayingPlayMode(),
            playModeGeneration: 2);
        var after = CreateSnapshot(
            UnityEditorLifecycleState.PlayMode,
            CreateStoppedPlayMode(),
            playModeGeneration: 3);
        var response = new IpcPlayTransitionResponse(CreateTerminalReference(), new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Exit,
            PlayLifecycleTransitionOutcome.Exited,
            before,
            After: after,
            Observed: null,
            ApplicationState: null));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }

}
