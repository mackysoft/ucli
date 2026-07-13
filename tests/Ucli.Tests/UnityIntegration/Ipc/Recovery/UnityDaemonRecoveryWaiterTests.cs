using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonRecoveryWaiterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenMatchingGuiSessionIsRecovering_DelaysAndReturnsTrue ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var waiter = CreateWaiter(
            session,
            CreateObservation(session, IpcEditorLifecycleState.DomainReloading),
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var delayTask = waiter.DelayIfRecoveringAsync(ResolvedUnityProjectContextTestFactory.Create(), deadline, CancellationToken.None).AsTask();
        Assert.False(delayTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        Assert.True(await delayTask);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenLifecycleSidecarIsMissing_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var waiter = CreateWaiter(
            DaemonSessionTestFactory.CreateEditorInstance(),
            observation: null,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(ResolvedUnityProjectContextTestFactory.Create(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenEditorInstanceMatchesAndStartTimeDiffers_DelaysAndReturnsTrue ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var observation = CreateObservation(
            session,
            IpcEditorLifecycleState.DomainReloading,
            processStartedAtUtc: session.ProcessStartedAtUtc!.Value.AddMilliseconds(1));
        var waiter = CreateWaiter(
            session,
            observation,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var delayTask = waiter.DelayIfRecoveringAsync(ResolvedUnityProjectContextTestFactory.Create(), deadline, CancellationToken.None).AsTask();
        Assert.False(delayTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        Assert.True(await delayTask);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenEditorInstanceDiffers_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var observation = CreateObservation(
            session,
            IpcEditorLifecycleState.DomainReloading,
            editorInstanceId: "other-editor-instance");
        var waiter = CreateWaiter(
            session,
            observation,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(ResolvedUnityProjectContextTestFactory.Create(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenEditorInstanceIdsAreMissing_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance() with
        {
            EditorInstanceId = null,
        };
        var observation = CreateObservation(session, IpcEditorLifecycleState.DomainReloading);
        var waiter = CreateWaiter(
            session,
            observation,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(ResolvedUnityProjectContextTestFactory.Create(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenProcessIdentityDiffers_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var waiter = CreateWaiter(
            session,
            CreateObservation(session, IpcEditorLifecycleState.Recovering),
            DaemonProcessIdentityAssessmentStatus.DifferentProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(ResolvedUnityProjectContextTestFactory.Create(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenSessionIsBatchmode_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance(editorMode: DaemonEditorMode.Batchmode);
        var waiter = CreateWaiter(
            session,
            CreateObservation(session, IpcEditorLifecycleState.DomainReloading),
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            timeProvider);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(ResolvedUnityProjectContextTestFactory.Create(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    private static UnityDaemonRecoveryWaiter CreateWaiter (
        DaemonSession session,
        DaemonLifecycleObservation? observation,
        DaemonProcessIdentityAssessmentStatus processStatus,
        TimeProvider timeProvider)
    {
        return new UnityDaemonRecoveryWaiter(
            new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            new RecordingDaemonLifecycleStore
            {
                ReadResult = DaemonLifecycleObservationReadResult.Success(observation),
            },
            new RecordingDaemonProcessIdentityAssessor(processStatus),
            timeProvider);
    }

    private static DaemonLifecycleObservation CreateObservation (
        DaemonSession session,
        IpcEditorLifecycleState lifecycleState,
        DateTimeOffset? processStartedAtUtc = null,
        string? editorInstanceId = null)
    {
        return new DaemonLifecycleObservation(
            processId: session.ProcessId!.Value,
            processStartedAtUtc: processStartedAtUtc ?? session.ProcessStartedAtUtc!.Value,
            state: new UnityEditorStateSnapshot(
                editorMode: session.EditorMode,
                lifecycleState: lifecycleState,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(1, 2, 0, 0),
                playMode: new IpcPlayModeSnapshot(
                    IpcPlayModeState.Stopped,
                    IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: DateTimeOffset.UtcNow,
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: editorInstanceId ?? session.EditorInstanceId);
    }

}
