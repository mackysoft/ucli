using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonRecoveryWaiterTests
{
    private static readonly Guid EditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid OtherEditorInstanceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenMatchingGuiSessionIsRecovering_DelaysAndReturnsTrue ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var waiter = CreateWaiter(
            session,
            CreateObservation(session, IpcEditorLifecycleState.DomainReloading),
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var delayTask = waiter.DelayIfRecoveringAsync(ResolvedUnityProjectContextTestFactory.Create(), deadline, CancellationToken.None).AsTask();
        Assert.False(delayTask.IsCompleted);

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);

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
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
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
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var delayTask = waiter.DelayIfRecoveringAsync(ResolvedUnityProjectContextTestFactory.Create(), deadline, CancellationToken.None).AsTask();
        Assert.False(delayTask.IsCompleted);

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);

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
            IpcEditorLifecycleState.Recovering,
            editorInstanceId: OtherEditorInstanceId,
            recoveryLease: new DaemonLifecycleRecoveryLease(
                session.SessionGenerationId,
                timeProvider.GetUtcNow() + DaemonLifecycleObservationTimings.DomainReloadRecoveryLeaseDuration));
        var waiter = CreateWaiter(
            session,
            observation,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(ResolvedUnityProjectContextTestFactory.Create(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenRecoveringObservationIsStale_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var observation = CreateObservation(
            session,
            IpcEditorLifecycleState.DomainReloading,
            observedAtUtc: timeProvider.GetUtcNow() - DaemonLifecycleObservationTimings.FreshnessWindow - TimeSpan.FromTicks(1));
        var waiter = CreateWaiter(
            session,
            observation,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var delayTask = waiter.DelayIfRecoveringAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                deadline,
                CancellationToken.None)
            .AsTask();

        Assert.False(await delayTask.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenRecoveryLeaseIsActiveBeyondFreshnessWindow_DelaysAndReturnsTrue ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var observedAtUtc = timeProvider.GetUtcNow()
            - DaemonLifecycleObservationTimings.FreshnessWindow
            - TimeSpan.FromSeconds(1);
        var observation = CreateObservation(
            session,
            IpcEditorLifecycleState.Recovering,
            observedAtUtc: observedAtUtc,
            recoveryLease: new DaemonLifecycleRecoveryLease(
                session.SessionGenerationId,
                timeProvider.GetUtcNow() + DaemonLifecycleObservationTimings.DomainReloadRecoveryLeaseDuration));
        var waiter = CreateWaiter(
            session,
            observation,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var delayTask = waiter.DelayIfRecoveringAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                deadline,
                CancellationToken.None)
            .AsTask();
        Assert.False(delayTask.IsCompleted);

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);

        Assert.True(await delayTask);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenRecoveryLeaseIsExpired_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var observedAtUtc = timeProvider.GetUtcNow()
            - DaemonLifecycleObservationTimings.FreshnessWindow
            - TimeSpan.FromSeconds(1);
        var observation = CreateObservation(
            session,
            IpcEditorLifecycleState.Recovering,
            observedAtUtc: observedAtUtc,
            recoveryLease: new DaemonLifecycleRecoveryLease(
                session.SessionGenerationId,
                timeProvider.GetUtcNow() - TimeSpan.FromTicks(1)));
        var waiter = CreateWaiter(
            session,
            observation,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            deadline,
            CancellationToken.None);

        Assert.False(result);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenRecoveryLeaseSessionGenerationDiffers_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var observedAtUtc = timeProvider.GetUtcNow()
            - DaemonLifecycleObservationTimings.FreshnessWindow
            - TimeSpan.FromSeconds(1);
        var observation = CreateObservation(
            session,
            IpcEditorLifecycleState.Recovering,
            observedAtUtc: observedAtUtc,
            recoveryLease: new DaemonLifecycleRecoveryLease(
                Guid.NewGuid(),
                timeProvider.GetUtcNow() + TimeSpan.FromSeconds(30)));
        var waiter = CreateWaiter(
            session,
            observation,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            deadline,
            CancellationToken.None);

        Assert.False(result);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenSessionEditorInstanceIdIsMissing_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.Create(editorMode: DaemonEditorMode.Gui);
        var observation = CreateObservation(
            session,
            IpcEditorLifecycleState.DomainReloading,
            editorInstanceId: EditorInstanceId);
        var waiter = CreateWaiter(
            session,
            observation,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
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
            CreateObservation(
                session,
                IpcEditorLifecycleState.Recovering,
                recoveryLease: new DaemonLifecycleRecoveryLease(
                    session.SessionGenerationId,
                    timeProvider.GetUtcNow() + TimeSpan.FromSeconds(30))),
            DaemonProcessIdentityAssessmentStatus.DifferentProcess);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(ResolvedUnityProjectContextTestFactory.Create(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DelayIfRecoveringAsync_WhenSessionIsBatchmode_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: ProjectIdentityInfoTestFactory.ProjectFingerprint,
            editorMode: DaemonEditorMode.Batchmode,
            ownerKind: DaemonSessionOwnerKind.Cli,
            canShutdownProcess: true);
        var waiter = CreateWaiter(
            session,
            observation: null,
            processStatus: DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = await waiter.DelayIfRecoveringAsync(ResolvedUnityProjectContextTestFactory.Create(), deadline, CancellationToken.None);

        Assert.False(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DelayIfRecoveringAsync_WhenStoreReadDoesNotQuiesce_ReturnsFalseAtDeadline (
        bool blockLifecycleRead,
        bool blockCancellationCallback)
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResultTestFactory.Found(session),
        };
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(
                CreateObservation(session, IpcEditorLifecycleState.DomainReloading)),
        };
        IBlockingReadOperation blockingReadOperation;
        if (blockLifecycleRead)
        {
            var lifecycleReadOperation = new BlockingReadOperation<DaemonLifecycleObservationReadResult>(
                lifecycleStore.ReadResult,
                blockCancellationCallback);
            lifecycleStore.ReadAsyncHandler = (_, _, cancellationToken) =>
                lifecycleReadOperation.ExecuteAsync(cancellationToken);
            blockingReadOperation = lifecycleReadOperation;
        }
        else
        {
            var sessionReadOperation = new BlockingReadOperation<DaemonSessionReadResult>(
                sessionStore.ReadResult,
                blockCancellationCallback);
            sessionStore.ReadAsyncHandler = (_, _, cancellationToken) =>
                sessionReadOperation.ExecuteAsync(cancellationToken);
            blockingReadOperation = sessionReadOperation;
        }

        var waiter = new UnityDaemonRecoveryWaiter(
            sessionStore,
            lifecycleStore,
            new RecordingDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess));
        var timeout = TimeSpan.FromSeconds(5);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var waitTask = waiter.DelayIfRecoveringAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                deadline,
                CancellationToken.None)
            .AsTask();

        try
        {
            await blockingReadOperation.Started.WaitAsync(TimeSpan.FromSeconds(1));
            await timeProvider
                .WaitForTimerDueWithinAsync(timeout)
                .WaitAsync(TimeSpan.FromSeconds(1));

            timeProvider.Advance(timeout);
            if (blockCancellationCallback)
            {
                await blockingReadOperation.CancellationCallbackStarted.WaitAsync(TimeSpan.FromSeconds(1));
            }

            var completedTask = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(waitTask, completedTask);
            Assert.False(await waitTask);
        }
        finally
        {
            blockingReadOperation.ReleaseCancellationCallback();
            blockingReadOperation.ReleaseOperation();
        }
    }

    private static UnityDaemonRecoveryWaiter CreateWaiter (
        DaemonSession session,
        DaemonLifecycleObservation? observation,
        DaemonProcessIdentityAssessmentStatus processStatus)
    {
        return new UnityDaemonRecoveryWaiter(
            new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            },
            new RecordingDaemonLifecycleStore
            {
                ReadResult = DaemonLifecycleObservationReadResult.Success(observation),
            },
            new RecordingDaemonProcessIdentityAssessor(processStatus));
    }

    private static DaemonLifecycleObservation CreateObservation (
        DaemonSession session,
        IpcEditorLifecycleState lifecycleState,
        DateTimeOffset? processStartedAtUtc = null,
        Guid? editorInstanceId = null,
        DateTimeOffset? observedAtUtc = null,
        DaemonLifecycleRecoveryLease? recoveryLease = null)
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
            observedAtUtc: observedAtUtc ?? DateTimeOffset.UnixEpoch,
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: editorInstanceId
                ?? session.EditorInstanceId
                ?? throw new InvalidOperationException("A valid Editor instance identifier is required by the test observation."),
            recoveryLease: recoveryLease);
    }

    private interface IBlockingReadOperation
    {
        Task Started { get; }

        Task CancellationCallbackStarted { get; }

        void ReleaseCancellationCallback ();

        void ReleaseOperation ();
    }

    private sealed class BlockingReadOperation<T> : IBlockingReadOperation
    {
        private readonly T result;

        private readonly bool blockCancellationCallback;

        private readonly TaskCompletionSource<bool> startedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> cancellationCallbackStartedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> cancellationCallbackReleaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> operationReleaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingReadOperation (
            T result,
            bool blockCancellationCallback)
        {
            this.result = result;
            this.blockCancellationCallback = blockCancellationCallback;
        }

        public Task Started => startedSource.Task;

        public Task CancellationCallbackStarted => cancellationCallbackStartedSource.Task;

        public async ValueTask<T> ExecuteAsync (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            startedSource.TrySetResult(true);
            using var cancellationRegistration = blockCancellationCallback
                ? cancellationToken.Register(() =>
                {
                    cancellationCallbackStartedSource.TrySetResult(true);
                    cancellationCallbackReleaseSource.Task.GetAwaiter().GetResult();
                })
                : default;
            await operationReleaseSource.Task;
            return result;
        }

        public void ReleaseCancellationCallback ()
        {
            cancellationCallbackReleaseSource.TrySetResult(true);
        }

        public void ReleaseOperation ()
        {
            operationReleaseSource.TrySetResult(true);
        }
    }

}
