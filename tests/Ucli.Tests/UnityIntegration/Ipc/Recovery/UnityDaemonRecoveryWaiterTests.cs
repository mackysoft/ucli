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
            CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading),
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
        var observation = CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading) with
        {
            ProcessStartedAtUtc = session.ProcessStartedAtUtc!.Value.AddMilliseconds(1),
        };
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
        var observation = CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading) with
        {
            EditorInstanceId = "other-editor-instance",
        };
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
        var observation = CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading);
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
            CreateObservation(session, IpcEditorLifecycleStateCodec.Recovering),
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
        var session = DaemonSessionTestFactory.CreateEditorInstance(editorMode: "batchmode");
        var waiter = CreateWaiter(
            session,
            CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading),
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
        string lifecycleState)
    {
        return new DaemonLifecycleObservation(
            ProcessId: session.ProcessId!.Value,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            EditorMode: session.EditorMode,
            LifecycleState: lifecycleState,
            BlockingReason: IpcEditorBlockingReasonCodec.DomainReload,
            CompileState: IpcCompileStateCodec.Ready,
            CompileGeneration: "1",
            DomainReloadGeneration: "2",
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ActionRequired: null,
            PrimaryDiagnostic: null)
        {
            EditorInstanceId = session.EditorInstanceId,
        };
    }

}
