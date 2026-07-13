using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonLifecycleObservationTests
{
    private static readonly Guid EditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenStateIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => new DaemonLifecycleObservation(
            processId: 1234,
            processStartedAtUtc: DateTimeOffset.UnixEpoch,
            state: null!,
            observedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(1),
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: EditorInstanceId));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenProcessIdIsNotPositive_ThrowsArgumentOutOfRangeException (int processId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateObservation(
            IpcEditorLifecycleState.Ready,
            processId: processId));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenProcessStartedAtUtcIsDefault_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateObservation(
            IpcEditorLifecycleState.Ready,
            processStartedAtUtc: new DateTimeOffset()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenObservedAtUtcIsDefault_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateObservation(
            IpcEditorLifecycleState.Ready,
            observedAtUtc: new DateTimeOffset()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenEditorInstanceIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new DaemonLifecycleObservation(
            processId: 1234,
            processStartedAtUtc: DateTimeOffset.UnixEpoch,
            state: CreateState(IpcEditorLifecycleState.Ready),
            observedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(1),
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: Guid.Empty));

        Assert.Equal("editorInstanceId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void LifecycleDerivedValues_WhenLifecycleStateChanges_FollowCurrentState ()
    {
        var ready = CreateObservation(IpcEditorLifecycleState.Ready);
        var recovering = CreateObservation(IpcEditorLifecycleState.Recovering);

        Assert.Null(ready.BlockingReason);
        Assert.True(ready.CanAcceptExecutionRequests);
        Assert.Equal(IpcEditorBlockingReason.Recovery, recovering.BlockingReason);
        Assert.False(recovering.CanAcceptExecutionRequests);
    }

    private static DaemonLifecycleObservation CreateObservation (
        IpcEditorLifecycleState lifecycleState,
        int processId = 1234,
        DateTimeOffset? processStartedAtUtc = null,
        DateTimeOffset? observedAtUtc = null)
    {
        return new DaemonLifecycleObservation(
            processId: processId,
            processStartedAtUtc: processStartedAtUtc ?? DateTimeOffset.UnixEpoch,
            state: CreateState(lifecycleState),
            observedAtUtc: observedAtUtc ?? DateTimeOffset.UnixEpoch.AddSeconds(1),
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: EditorInstanceId);
    }

    private static UnityEditorStateSnapshot CreateState (IpcEditorLifecycleState lifecycleState)
    {
        return new UnityEditorStateSnapshot(
            editorMode: DaemonEditorMode.Gui,
            lifecycleState: lifecycleState,
            compileState: IpcCompileState.Ready,
            generations: new IpcUnityGenerationSnapshot(1, 2, 0, 0),
            playMode: new IpcPlayModeSnapshot(
                IpcPlayModeState.Stopped,
                IpcPlayModeTransition.None,
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false));
    }
}
