using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStopOperationTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStopOperationTimeoutTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenSessionReadIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCompletion = new TaskCompletionSource<DaemonSessionReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycleLease = new RecordingAsyncDisposable();
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadAsyncHandler = async (_, _, cancellationToken) =>
            {
                _ = cancellationToken.UnsafeRegister(
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
        var shutdownClient = new RecordingDaemonShutdownClient();
        var processTerminationService = new RecordingDaemonProcessTerminationService();
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = CreateOperation(
            lifecycleLockProvider: new StubProjectLifecycleLockProvider((_, _, _) => lifecycleLease),
            sessionStore: sessionStore,
            shutdownClient: shutdownClient,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner,
            timeProvider: timeProvider);
        var unityProject = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-stop-session-read-timeout"));
        var timeout = TimeSpan.FromSeconds(1);

        var resultTask = operation.StopAsync(
                unityProject,
                timeout,
                cancellationToken: CancellationToken.None)
            .AsTask();
        try
        {
            await TestAwaiter.WaitAsync(readStarted.Task, "Non-cooperative stop session read", SignalWaitTimeout);
            await TestAwaiter.WaitAsync(
                timeProvider.WaitForTimerDueWithinAsync(timeout),
                "Stop session read deadline timer",
                SignalWaitTimeout);
            timeProvider.Advance(timeout);
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Stop session read deadline result",
                SignalWaitTimeout);
            await TestAwaiter.WaitAsync(
                readCancellationObserved.Task,
                "Stop session read cancellation",
                SignalWaitTimeout);

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
            Assert.Equal(1, lifecycleLease.DisposeCount);
            Assert.Empty(shutdownClient.Invocations);
            Assert.Empty(processTerminationService.Invocations);
            Assert.Empty(artifactCleaner.Invocations);
        }
        finally
        {
            readCompletion.TrySetResult(DaemonSessionReadResult.Missing());
            await TestAwaiter.WaitAsync(readFinished.Task, "Stop session read completion", SignalWaitTimeout);
        }
    }

    private sealed class RecordingAsyncDisposable : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync ()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
