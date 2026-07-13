using MackySoft.Ucli.Application.Shared.CommandContracts.Projection;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Shared.CommandContracts.Projection;

public sealed class LifecycleProjectionFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenPlayModeIsMissing_ReturnsNullPlayMode ()
    {
        var projection = LifecycleProjectionFactory.Create(CreatePing(playMode: null));

        Assert.Null(projection.PlayMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenLifecycleLiteralsAreCanonical_ReturnsTypedLifecycleValues ()
    {
        var projection = LifecycleProjectionFactory.Create(CreatePing(playMode: null));

        Assert.Equal(IpcEditorLifecycleState.Ready, projection.LifecycleState);
        Assert.Null(projection.BlockingReason);
        Assert.Equal(IpcCompileState.Ready, projection.CompileState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenLifecycleLiteralHasOuterWhitespace_ReturnsNullLifecycleState ()
    {
        var projection = LifecycleProjectionFactory.Create(CreatePing(playMode: null) with
        {
            LifecycleState = " ready ",
        });

        Assert.Null(projection.LifecycleState);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("ready", null, false)]
    [InlineData("ready", "busy", true)]
    [InlineData("compiling", null, false)]
    [InlineData("compiling", "busy", false)]
    [InlineData("compiling", "compile", true)]
    public void Create_WhenLifecycleTupleIsInconsistent_FailsClosed (
        string lifecycleState,
        string? blockingReason,
        bool canAcceptExecutionRequests)
    {
        var projection = LifecycleProjectionFactory.Create(CreatePing(playMode: null) with
        {
            LifecycleState = lifecycleState,
            BlockingReason = blockingReason,
            CanAcceptExecutionRequests = canAcceptExecutionRequests,
        });

        Assert.Null(projection.LifecycleState);
        Assert.Null(projection.BlockingReason);
        Assert.False(projection.CanAcceptExecutionRequests);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("unsupported", "none")]
    [InlineData("playing", "unsupported")]
    public void Create_WhenPlayModeHasUnsupportedLiteral_ReturnsNullPlayMode (
        string state,
        string transition)
    {
        var projection = LifecycleProjectionFactory.Create(CreatePing(new IpcPlayModeSnapshot(
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
        var projection = LifecycleProjectionFactory.Create(CreatePing(new IpcPlayModeSnapshot(
            State: " playing ",
            Transition: " none ",
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true,
            Generation: "   ")));

        Assert.NotNull(projection.PlayMode);
        Assert.Equal("playing", projection.PlayMode.State);
        Assert.Equal("none", projection.PlayMode.Transition);
        Assert.Null(projection.PlayMode.Generation);
    }

    private static IpcPingResponse CreatePing (IpcPlayModeSnapshot? playMode)
    {
        return new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: "ready",
            LifecycleState: "ready",
            CanAcceptExecutionRequests: true,
            PlayMode: playMode);
    }
}
