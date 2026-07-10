using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStopOperationTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStopOperationTimeoutTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenSessionReadIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCompletion = new TaskCompletionSource<DaemonSessionReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationCallbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationCallbackCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var allowCancellationCallbackCompletion = new ManualResetEventSlim();
        var lifecycleLease = new RecordingAsyncDisposable();
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadAsyncHandler = (_, _, cancellationToken) =>
            {
                _ = cancellationToken.Register(() =>
                {
                    cancellationCallbackStarted.TrySetResult();
                    allowCancellationCallbackCompletion.Wait();
                    cancellationCallbackCompleted.TrySetResult();
                });
                readStarted.TrySetResult();
                return new ValueTask<DaemonSessionReadResult>(readCompletion.Task);
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
            "fingerprint-stop-session-read-timeout");
        var timeout = TimeSpan.FromSeconds(1);

        var resultTask = operation.StopAsync(
                unityProject,
                timeout,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(timeout);
            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));
            await cancellationCallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
            Assert.Equal(1, lifecycleLease.DisposeCount);
            Assert.Empty(shutdownClient.Invocations);
            Assert.Empty(processTerminationService.Invocations);
            Assert.Empty(artifactCleaner.Invocations);
        }
        finally
        {
            allowCancellationCallbackCompletion.Set();
            readCompletion.TrySetResult(DaemonSessionReadResult.Success(null));
            if (cancellationCallbackStarted.Task.IsCompleted)
            {
                await cancellationCallbackCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            }
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
