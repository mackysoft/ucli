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
    public async Task StartAsync_WhenExitedDoesNotChangeGeneration_ReturnsStateUnknown ()
    {
        var before = CreateSnapshot(
            UnityEditorLifecycleState.PlayMode,
            CreatePlayingPlayMode(),
            playModeGeneration: 2);
        var after = CreateSnapshot(
            UnityEditorLifecycleState.Ready,
            CreateStoppedPlayMode(),
            playModeGeneration: 2);
        var response = new IpcPlayTransitionResponse(
            CreateTerminalReference(),
            new PlayLifecycleTransitionResult(
                PlayLifecycleTransitionCommand.Exit,
                PlayLifecycleTransitionOutcome.Exited,
                before,
                after,
                Observed: null,
                ApplicationState: null));
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(CreateResponse(response), CreateStartBinding()));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.StartAsync(
            await CreateStartInvocationAsync(requestExecutor));

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }
}
