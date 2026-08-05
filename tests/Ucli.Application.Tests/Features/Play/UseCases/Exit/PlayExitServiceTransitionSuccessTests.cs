using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayExitServiceTestSupport;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayExitServiceTransitionSuccessTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExitSucceeds_ReturnsReadyStoppedPayloadAndTransition ()
    {
        var context = PlayProjectContext;
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateExitedResponse())));
        var service = CreateService(context, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(AbsolutePath.Parse(ProjectPathTestValues.RepositoryUnityProject), 1500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayExitExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(context.UnityProject.UnityProjectRoot.Value, output.Project.ProjectPath);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal(UnityEditorMode.Gui, output.EditorMode);
        Assert.Equal(UnityEditorLifecycleState.Ready, output.LifecycleState);
        Assert.Null(output.BlockingReason);
        Assert.True(output.CanAcceptExecutionRequests);
        Assert.Equal(UnityEditorPlayModeState.Stopped, output.PlayMode.State);
        Assert.Equal(3, output.Generations!.PlayModeGeneration);
        Assert.Equal(1500, output.TimeoutMilliseconds);
        Assert.Equal(PlayLifecycleTransitionCommand.Exit, output.Transition.Transition);
        Assert.Equal(PlayLifecycleTransitionOutcome.Exited, output.Transition.Result);
        Assert.NotNull(output.Transition.Before);
        Assert.NotNull(output.Transition.After);

        UnityRequestExecutorInvocationAssert.PlayExitOnce(
            requestExecutor,
            TimeSpan.FromMilliseconds(4500));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAlreadyStopped_ReturnsAlreadyExitedWithoutGenerationChange ()
    {
        var before = CreateSnapshot(
            UnityEditorLifecycleState.Compiling,
            CreateStoppedPlayMode(),
            playModeGeneration: 9);
        var response = new IpcPlayTransitionResponse(CreateTerminalReference(), new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Exit,
            PlayLifecycleTransitionOutcome.AlreadyExited,
            before,
            After: before,
            Observed: null,
            ApplicationState: null));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayExitExecutionOutput>(result.Output);
        Assert.Equal(PlayLifecycleTransitionOutcome.AlreadyExited, output.Transition.Result);
        Assert.Equal(UnityEditorLifecycleState.Compiling, output.LifecycleState);
        Assert.Equal(9, output.Transition.Before.Generations!.PlayModeGeneration);
        Assert.Equal(9, output.Transition.After!.Generations!.PlayModeGeneration);
    }
}
