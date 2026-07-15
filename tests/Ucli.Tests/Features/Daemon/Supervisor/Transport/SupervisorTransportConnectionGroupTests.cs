namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorTransportConnectionGroupTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandlerCompletion_WhenTransportCleanupBlocks_KeepsAdmissionSlotUntilCleanupCompletes ()
    {
        var fatalException = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var group = new SupervisorTransportConnectionGroup(
            static stream => stream.Dispose(),
            exception => fatalException.TrySetResult(exception),
            TimeProvider.System);
        using var firstStream = new BlockingDisposeStream();
        using var secondStream = new MemoryStream();
        var secondHandlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            Assert.True(group.TryStart(
                firstStream,
                static (_, _) => Task.CompletedTask,
                maximumActiveConnections: 1,
                CancellationToken.None));
            await TestAwaiter.WaitAsync(
                firstStream.DisposeStarted,
                "First supervisor connection cleanup",
                SignalWaitTimeout);

            Assert.False(group.TryStart(
                secondStream,
                (_, _) =>
                {
                    secondHandlerEntered.TrySetResult();
                    return Task.CompletedTask;
                },
                maximumActiveConnections: 1,
                CancellationToken.None));
            var drainTask = group.DrainAsync(SignalWaitTimeout);
            Assert.False(drainTask.IsCompleted);

            firstStream.CompleteDispose();
            await TestAwaiter.WaitAsync(
                drainTask,
                "Supervisor connection cleanup drain",
                SignalWaitTimeout);
            Assert.True(group.TryStart(
                secondStream,
                (_, _) =>
                {
                    secondHandlerEntered.TrySetResult();
                    return Task.CompletedTask;
                },
                maximumActiveConnections: 1,
                CancellationToken.None));
            await TestAwaiter.WaitAsync(
                secondHandlerEntered.Task,
                "Second supervisor connection handler",
                SignalWaitTimeout);
            Assert.False(fatalException.Task.IsCompleted);
        }
        finally
        {
            firstStream.CompleteDispose();
            group.Release();
            await group.DrainAsync(SignalWaitTimeout);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TransportCleanup_WhenReleaseThrows_RemovesConnectionAndReportsFatalFailure ()
    {
        var fatalException = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCount = 0;
        var group = new SupervisorTransportConnectionGroup(
            stream =>
            {
                if (Interlocked.Increment(ref releaseCount) == 1)
                {
                    throw new InvalidOperationException("Injected transport cleanup failure.");
                }

                stream.Dispose();
            },
            exception => fatalException.TrySetResult(exception),
            TimeProvider.System);
        using var firstStream = new MemoryStream();
        using var secondStream = new MemoryStream();

        try
        {
            Assert.True(group.TryStart(
                firstStream,
                static (_, _) => Task.CompletedTask,
                maximumActiveConnections: 1,
                CancellationToken.None));
            var observedException = await fatalException.Task.WaitAsync(SignalWaitTimeout);
            await group.DrainAsync(SignalWaitTimeout);

            Assert.Contains("Injected transport cleanup failure", observedException.Message, StringComparison.Ordinal);
            Assert.True(group.TryStart(
                secondStream,
                static (_, _) => Task.CompletedTask,
                maximumActiveConnections: 1,
                CancellationToken.None));
        }
        finally
        {
            group.Release();
            await group.DrainAsync(SignalWaitTimeout);
        }
    }

    private sealed class BlockingDisposeStream : MemoryStream
    {
        private readonly TaskCompletionSource disposeStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource disposeCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task DisposeStarted => disposeStarted.Task;

        public void CompleteDispose ()
        {
            disposeCompletion.TrySetResult();
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                disposeStarted.TrySetResult();
                disposeCompletion.Task.GetAwaiter().GetResult();
            }

            base.Dispose(disposing);
        }
    }
}
