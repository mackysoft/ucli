namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

public sealed class DaemonExistingSessionGateServiceFailureTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingIgnoresCancellation_ReturnsAtDeadlineAndRejectsLateSuccess ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pingCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pingCompletion = new TaskCompletionSource<IpcUnityEditorObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pingFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pingClient = new RecordingDaemonPingInfoClient
        {
            PingSessionAndReadHandler = async (_, _, _, _, cancellationToken) =>
            {
                _ = cancellationToken.UnsafeRegister(
                    static state => ((TaskCompletionSource)state!).TrySetResult(),
                    pingCancellationObserved);
                pingStarted.TrySetResult();
                try
                {
                    return await pingCompletion.Task.ConfigureAwait(false);
                }
                finally
                {
                    pingFinished.TrySetResult();
                }
            },
        };
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient);
        var timeout = TimeSpan.FromMilliseconds(500);
        var resultTask = service.TryHandleExistingSessionAsync(
                ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
                    ProjectFingerprintTestFactory.Create("fingerprint-existing-non-cooperative-ping")),
                DaemonSessionTestFactory.Create(processId: 4019),
                ExecutionDeadline.Start(timeout, timeProvider),
                editorMode: null,
                cancellationToken: CancellationToken.None)
            .AsTask();

        try
        {
            await TestAwaiter.WaitAsync(pingStarted.Task, "Non-cooperative existing-session ping", SignalWaitTimeout);
            await TestAwaiter.WaitAsync(
                timeProvider.WaitForTimerDueWithinAsync(timeout),
                "Existing-session ping deadline timer",
                SignalWaitTimeout);
            timeProvider.Advance(timeout);

            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Non-cooperative existing-session ping deadline result",
                SignalWaitTimeout);
            await TestAwaiter.WaitAsync(
                pingCancellationObserved.Task,
                "Non-cooperative existing-session ping cancellation",
                SignalWaitTimeout);

            Assert.NotNull(result);
            Assert.Equal(DaemonStartStatus.Failed, result!.Status);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);

            pingCompletion.TrySetResult(DaemonExistingSessionGateServiceTestSupport.CreateReadyPingResponse());
            await TestAwaiter.WaitAsync(pingFinished.Task, "Late existing-session ping completion", SignalWaitTimeout);
            Assert.Equal(DaemonStartStatus.Failed, (await resultTask)!.Status);
        }
        finally
        {
            pingCompletion.TrySetResult(DaemonExistingSessionGateServiceTestSupport.CreateReadyPingResponse());
            await TestAwaiter.WaitAsync(pingFinished.Task, "Existing-session ping cleanup", SignalWaitTimeout);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenReplacementSessionReadIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromMilliseconds(500);
        var session = DaemonSessionTestFactory.Create(processId: 4023);
        var sessionStore = new NonCooperativeBlockingDaemonSessionStore(
            blockOnCall: 1,
            DaemonSessionReadResultTestFactory.Found(session));
        var classifier = new DelegatingDaemonReachabilityClassifier(
            isNotRunning: static _ => false,
            isSessionTokenInvalid: static _ => false,
            isRetryableBeforeRequestWrite: static _ => false,
            isRecoverableResponseInterruption: static exception => exception is TimeoutException);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(
                new TimeoutException("response attempt timed out")),
            reachabilityClassifier: classifier,
            daemonSessionStore: sessionStore);
        var resultTask = service.TryHandleExistingSessionAsync(
                ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(session.ProjectFingerprint),
                session,
                ExecutionDeadline.Start(timeout, timeProvider),
                editorMode: null,
                cancellationToken: CancellationToken.None)
            .AsTask();

        await sessionStore.Blocked.WaitAsync(SignalWaitTimeout);
        try
        {
            await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(SignalWaitTimeout);
            timeProvider.Advance(timeout);
            var result = await resultTask.WaitAsync(SignalWaitTimeout);

            Assert.NotNull(result);
            Assert.Equal(DaemonStartStatus.Failed, result!.Status);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        }
        finally
        {
            sessionStore.Release();
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenLifecycleReadIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromMilliseconds(500);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-existing-lifecycle-read-timeout"));
        var session = DaemonExistingSessionGateServiceTestSupport.CreateRecoveringGuiSession(
            processId: 4024,
            projectFingerprint: context.ProjectFingerprint,
            editorInstanceId: Guid.NewGuid());
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readRelease = new TaskCompletionSource<DaemonLifecycleObservationReadResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadAsyncHandler = async (_, _, _) =>
            {
                readStarted.TrySetResult();
                return await readRelease.Task.ConfigureAwait(false);
            },
        };
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new TimeoutException("endpoint timed out")),
            lifecycleStore: lifecycleStore,
            processIdentityAssessor: DaemonExistingSessionGateServiceTestSupport.CreateMatchingProcessIdentityAssessor(session));
        var resultTask = service.TryHandleExistingSessionAsync(
                context,
                session,
                ExecutionDeadline.Start(timeout, timeProvider),
                editorMode: null,
                cancellationToken: CancellationToken.None)
            .AsTask();

        await readStarted.Task.WaitAsync(SignalWaitTimeout);
        try
        {
            await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(SignalWaitTimeout);
            timeProvider.Advance(timeout);
            var result = await resultTask.WaitAsync(SignalWaitTimeout);

            Assert.NotNull(result);
            Assert.Equal(DaemonStartStatus.Failed, result!.Status);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        }
        finally
        {
            readRelease.TrySetResult(DaemonLifecycleObservationReadResult.Success(null));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingTimesOut_ReturnsTimeoutFailure ()
    {
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new TimeoutException("timeout")));

        var result = await service.TryHandleExistingSessionAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-timeout")),
            DaemonSessionTestFactory.Create(processId: 4002),
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new ManualTimeProvider()),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingThrowsUnexpectedError_ReturnsInternalFailure ()
    {
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new InvalidOperationException("unexpected")));

        var result = await service.TryHandleExistingSessionAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-unexpected")),
            DaemonSessionTestFactory.Create(processId: 4005),
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new ManualTimeProvider()),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
    }
}
