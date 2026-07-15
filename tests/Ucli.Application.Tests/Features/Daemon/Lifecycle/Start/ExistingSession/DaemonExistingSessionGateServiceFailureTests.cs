namespace MackySoft.Ucli.Application.Tests.Daemon;

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
            PingAndReadHandler = async (_, _, _, _, cancellationToken) =>
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
