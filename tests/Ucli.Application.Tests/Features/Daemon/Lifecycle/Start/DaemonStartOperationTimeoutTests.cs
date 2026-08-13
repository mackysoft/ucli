using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStartOperationTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartOperationTimeoutTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionReadIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeout = TimeSpan.FromMilliseconds(500);
        var timeProvider = new DeadlineTimerObservingTimeProvider(
            new FakeTimeProvider(DateTimeOffset.UnixEpoch),
            timeout);
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCompletion = new TaskCompletionSource<DaemonSessionReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadAsyncHandler = async (_, _, cancellationToken) =>
            {
                using var cancellationRegistration = cancellationToken.UnsafeRegister(
                    static state => ((TaskCompletionSource)state!).TrySetResult(),
                    readCancellationObserved);
                readStarted.TrySetResult();
                try
                {
                    return await readCompletion.Task.ConfigureAwait(false);
                }
                finally
                {
                    readFinished.TrySetResult();
                }
            },
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: new RecordingDaemonLaunchService(),
            timeProvider: timeProvider);
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-start-session-read-timeout"));

        var resultTask = operation.StartAsync(
                unityProject,
                ExecutionDeadline.Start(timeout, timeProvider),
                editorMode: null,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();

        try
        {
            await timeProvider.WaitForDeadlineTimerRegistrationAsync();
            await readStarted.Task;
            Assert.False(resultTask.IsCompleted);
            Assert.False(readCompletion.Task.IsCompleted);

            timeProvider.Advance(timeout);
            await readCancellationObserved.Task;
            Assert.False(readCompletion.Task.IsCompleted);
            Assert.False(readFinished.Task.IsCompleted);

            var result = await resultTask;

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        }
        finally
        {
            readCompletion.TrySetResult(DaemonSessionReadResult.Missing());
            await readFinished.Task;
        }
    }

    private sealed class DeadlineTimerObservingTimeProvider : TimeProvider
    {
        private readonly FakeTimeProvider inner;

        private readonly TimeSpan deadlineTimeout;

        private readonly TaskCompletionSource deadlineTimerRegistered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DeadlineTimerObservingTimeProvider (
            FakeTimeProvider inner,
            TimeSpan deadlineTimeout)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.deadlineTimeout = deadlineTimeout;
        }

        public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

        public override long TimestampFrequency => inner.TimestampFrequency;

        public override DateTimeOffset GetUtcNow ()
        {
            return inner.GetUtcNow();
        }

        public override long GetTimestamp ()
        {
            return inner.GetTimestamp();
        }

        public void Advance (TimeSpan elapsed)
        {
            inner.Advance(elapsed);
        }

        public override ITimer CreateTimer (
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = inner.CreateTimer(callback, state, dueTime, period);
            if (dueTime == deadlineTimeout && period == Timeout.InfiniteTimeSpan)
            {
                deadlineTimerRegistered.TrySetResult();
            }

            return timer;
        }

        public Task WaitForDeadlineTimerRegistrationAsync ()
        {
            return deadlineTimerRegistered.Task;
        }
    }
}
