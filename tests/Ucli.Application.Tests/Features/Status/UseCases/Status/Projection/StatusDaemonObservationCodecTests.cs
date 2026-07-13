using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Projection;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Status;

public sealed class StatusDaemonObservationCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateWithoutPing_ReturnsObservationWithNullPingFields ()
    {
        var actual = StatusDaemonObservationCodec.CreateWithoutPing(DaemonStatusKind.NotRunning);

        Assert.Equal(DaemonStatusKind.NotRunning, actual.DaemonStatus);
        Assert.Null(actual.ServerVersion);
        Assert.Null(actual.LifecycleState);
        Assert.Null(actual.BlockingReason);
        Assert.Null(actual.CompileState);
        Assert.Null(actual.Generations);
        Assert.False(actual.CanAcceptExecutionRequests);
        Assert.Null(actual.EditorMode);
        Assert.Null(actual.PlayMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateFromPing_ProjectsTypedEditorStateSnapshot ()
    {
        var observedAtUtc = new DateTimeOffset(2026, 7, 13, 1, 2, 3, TimeSpan.Zero);
        var pingResponse = new IpcUnityEditorObservation(
            serverVersion: " 0.5.0 ",
            unityVersion: "2022.3.5f1",
            projectFingerprint: "project-fingerprint",
            state: new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Batchmode,
                lifecycleState: IpcEditorLifecycleState.Compiling,
                compileState: IpcCompileState.Compiling,
                generations: new IpcUnityGenerationSnapshot(
                    CompileGeneration: 42,
                    DomainReloadGeneration: 17,
                    AssetRefreshGeneration: 11,
                    PlayModeGeneration: 9),
                playMode: new IpcPlayModeSnapshot(
                    State: IpcPlayModeState.Entering,
                    Transition: IpcPlayModeTransition.Entering,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: true)),
            observedAtUtc: observedAtUtc);

        var actual = StatusDaemonObservationCodec.CreateFromPing(
            DaemonStatusKind.Running,
            pingResponse);

        Assert.Equal(DaemonStatusKind.Running, actual.DaemonStatus);
        Assert.Equal("0.5.0", actual.ServerVersion);
        Assert.Equal(IpcEditorLifecycleState.Compiling, actual.LifecycleState);
        Assert.Equal(IpcEditorBlockingReason.Compile, actual.BlockingReason);
        Assert.Equal(IpcCompileState.Compiling, actual.CompileState);
        Assert.NotNull(actual.Generations);
        Assert.Equal(42, actual.Generations.CompileGeneration);
        Assert.Equal(17, actual.Generations.DomainReloadGeneration);
        Assert.Equal(11, actual.Generations.AssetRefreshGeneration);
        Assert.Equal(9, actual.Generations.PlayModeGeneration);
        Assert.False(actual.CanAcceptExecutionRequests);
        Assert.Equal(DaemonEditorMode.Batchmode, actual.EditorMode);
        Assert.Equal(observedAtUtc, actual.ObservedAtUtc);
        Assert.NotNull(actual.PlayMode);
        Assert.Equal(IpcPlayModeState.Entering, actual.PlayMode.State);
        Assert.Equal(IpcPlayModeTransition.Entering, actual.PlayMode.Transition);
        Assert.False(actual.PlayMode.IsPlaying);
        Assert.True(actual.PlayMode.IsPlayingOrWillChangePlaymode);
    }
}
