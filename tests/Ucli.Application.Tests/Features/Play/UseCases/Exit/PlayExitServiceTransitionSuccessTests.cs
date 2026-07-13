using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayExitServiceTestSupport;

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

        var result = await service.ExecuteAsync(new PlayExitCommandInput("/repo/UnityProject", 1500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayExitExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(context.UnityProject.UnityProjectRoot, output.Project.ProjectPath);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal("gui", output.EditorMode);
        Assert.Equal("ready", output.LifecycleState);
        Assert.Null(output.BlockingReason);
        Assert.True(output.CanAcceptExecutionRequests);
        Assert.Equal("stopped", output.PlayMode.State);
        Assert.Equal("3", output.PlayMode.Generation);
        Assert.Equal(1500, output.TimeoutMilliseconds);
        Assert.Equal(IpcPlayTransitionCommandNames.Exit, output.Transition.Transition);
        Assert.Equal(IpcPlayTransitionResultNames.Exited, output.Transition.Result);
        Assert.NotNull(output.Transition.Before);
        Assert.NotNull(output.Transition.After);
        Assert.Null(output.Transition.Observed);
        Assert.Null(output.Transition.ApplicationState);

        UnityRequestExecutorInvocationAssert.PlayExitOnce(
            requestExecutor,
            TimeSpan.FromMilliseconds(2500),
            expectedPayloadTimeoutMilliseconds: 1500);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAlreadyStopped_ReturnsAlreadyExitedWithoutGenerationChange ()
    {
        var before = CreateSnapshot(
            "compiling",
            "compile",
            false,
            CreateStoppedPlayMode("9"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.AlreadyExited,
            before)
        {
            After = before,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayExitExecutionOutput>(result.Output);
        Assert.Equal(IpcPlayTransitionResultNames.AlreadyExited, output.Transition.Result);
        Assert.Equal("compiling", output.LifecycleState);
        Assert.Equal("9", output.Transition.Before.PlayMode!.Generation);
        Assert.Equal("9", output.Transition.After!.PlayMode!.Generation);
    }
}
