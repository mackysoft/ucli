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
                ExecutionDeadline.Start(timeout, timeProvider),
                cancellationToken: CancellationToken.None)
            .AsTask();
        try
        {
            await readStarted.Task.WaitAsync(SignalWaitTimeout);
            await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(SignalWaitTimeout);
            timeProvider.Advance(timeout);
            var result = await resultTask.WaitAsync(SignalWaitTimeout);
            await readCancellationObserved.Task.WaitAsync(SignalWaitTimeout);

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
            await readFinished.Task.WaitAsync(SignalWaitTimeout);
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
