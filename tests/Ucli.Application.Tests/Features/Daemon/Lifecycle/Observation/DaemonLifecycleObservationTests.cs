using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

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
            editorInstanceId: EditorInstanceId,
            recoveryLease: null));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenProcessIdIsNotPositive_ThrowsArgumentOutOfRangeException (int processId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateObservation(
            UnityEditorLifecycleState.Ready,
            processId: processId));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenProcessStartedAtUtcIsDefault_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateObservation(
            UnityEditorLifecycleState.Ready,
            processStartedAtUtc: new DateTimeOffset()));

        Assert.Equal("processStartedAtUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenObservedAtUtcIsDefault_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateObservation(
            UnityEditorLifecycleState.Ready,
            observedAtUtc: new DateTimeOffset()));

        Assert.Equal("observedAtUtc", exception.ParamName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenTimestampHasNonUtcOffset_ThrowsArgumentException (bool useProcessStartTimestamp)
    {
        var nonUtcTimestamp = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(9));

        var exception = Assert.Throws<ArgumentException>(() => CreateObservation(
            UnityEditorLifecycleState.Ready,
            processStartedAtUtc: useProcessStartTimestamp ? nonUtcTimestamp : null,
            observedAtUtc: useProcessStartTimestamp ? null : nonUtcTimestamp));

        Assert.Equal(
            useProcessStartTimestamp ? "processStartedAtUtc" : "observedAtUtc",
            exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenEditorInstanceIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new DaemonLifecycleObservation(
            processId: 1234,
            processStartedAtUtc: DateTimeOffset.UnixEpoch,
            state: CreateState(UnityEditorLifecycleState.Ready),
            observedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(1),
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: Guid.Empty,
            recoveryLease: null));

        Assert.Equal("editorInstanceId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenRecoveryLeaseIsAttachedToReadyObservation_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new DaemonLifecycleObservation(
            processId: 1234,
            processStartedAtUtc: DateTimeOffset.UnixEpoch,
            state: CreateState(UnityEditorLifecycleState.Ready),
            observedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(1),
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: EditorInstanceId,
            recoveryLease: new DaemonLifecycleRecoveryLease(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                DateTimeOffset.UnixEpoch.AddMinutes(1))));

        Assert.Equal("recoveryLease", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void LifecycleDerivedValues_WhenLifecycleStateChanges_FollowCurrentState ()
    {
        var ready = CreateObservation(UnityEditorLifecycleState.Ready);
        var recovering = CreateObservation(UnityEditorLifecycleState.Recovering);

        Assert.Null(ready.BlockingReason);
        Assert.True(ready.CanAcceptExecutionRequests);
        Assert.Equal(UnityEditorBlockingReason.Recovery, recovering.BlockingReason);
        Assert.False(recovering.CanAcceptExecutionRequests);
    }

    private static DaemonLifecycleObservation CreateObservation (
        UnityEditorLifecycleState lifecycleState,
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
            editorInstanceId: EditorInstanceId,
            recoveryLease: null);
    }

    private static UnityEditorStateSnapshot CreateState (UnityEditorLifecycleState lifecycleState)
    {
        return new UnityEditorStateSnapshot(
            editorMode: UnityEditorMode.Gui,
            lifecycleState: lifecycleState,
            compileState: UnityEditorCompileState.Ready,
            generations: new UnityEditorGenerationSnapshot(1, 2, 0, 0),
            playMode: new UnityEditorPlayModeSnapshot(
                UnityEditorPlayModeState.Stopped,
                UnityEditorPlayModeTransition.None,
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false));
    }
}
