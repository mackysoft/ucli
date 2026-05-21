using MackySoft.Ucli.Application.Shared.CommandContracts.Projection;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Shared.CommandContracts.Projection;

public sealed class PingLifecycleProjectionFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenPlayModeIsMissing_ReturnsNullPlayMode ()
    {
        var projection = PingLifecycleProjectionFactory.Create(CreatePing(playMode: null));

        Assert.Null(projection.PlayMode);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("unsupported", IpcPlayModeTransitionNames.None)]
    [InlineData(IpcPlayModeStateNames.Playing, "unsupported")]
    public void Create_WhenPlayModeHasUnsupportedLiteral_ReturnsNullPlayMode (
        string state,
        string transition)
    {
        var projection = PingLifecycleProjectionFactory.Create(CreatePing(new IpcPlayModeSnapshot(
            State: state,
            Transition: transition,
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true,
            Generation: "3")));

        Assert.Null(projection.PlayMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenPlayModeGenerationIsBlank_ReturnsPlayModeWithNullGeneration ()
    {
        var projection = PingLifecycleProjectionFactory.Create(CreatePing(new IpcPlayModeSnapshot(
            State: $" {IpcPlayModeStateNames.Playing} ",
            Transition: $" {IpcPlayModeTransitionNames.None} ",
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true,
            Generation: "   ")));

        Assert.NotNull(projection.PlayMode);
        Assert.Equal(IpcPlayModeStateNames.Playing, projection.PlayMode.State);
        Assert.Equal(IpcPlayModeTransitionNames.None, projection.PlayMode.Transition);
        Assert.Null(projection.PlayMode.Generation);
    }

    private static IpcPingResponse CreatePing (IpcPlayModeSnapshot? playMode)
    {
        return new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: DaemonEditorModeValues.Batchmode,
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: IpcCompileStateCodec.Ready,
            LifecycleState: IpcEditorLifecycleStateCodec.Ready,
            CanAcceptExecutionRequests: true,
            PlayMode: playMode);
    }
}
