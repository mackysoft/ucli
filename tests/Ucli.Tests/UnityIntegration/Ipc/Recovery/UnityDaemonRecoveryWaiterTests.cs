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
        var observation = CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading) with
        {
            ProcessStartedAtUtc = session.ProcessStartedAtUtc!.Value.AddMilliseconds(1),
        };
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
        var observation = CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading) with
        {
            EditorInstanceId = "other-editor-instance",
        };
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
    public async Task DelayIfRecoveringAsync_WhenEditorInstanceIdsAreMissing_ReturnsFalseWithoutDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.CreateUserOwned(
            editorMode: "gui",
            endpointAddress: "/tmp/ucli.sock",
            editorInstanceId: null);
        var observation = CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading);
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
            CreateObservation(session, IpcEditorLifecycleStateCodec.Recovering),
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
        var session = DaemonSessionTestFactory.Create(editorMode: "batchmode");
        var waiter = CreateWaiter(
            session,
            CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading),
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess);
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
                CreateObservation(session, IpcEditorLifecycleStateCodec.DomainReloading)),
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
        string lifecycleState)
    {
        return new DaemonLifecycleObservation(
            ProcessId: session.ProcessId!.Value,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            EditorMode: ContractLiteralCodec.ToValue(session.EditorMode),
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
