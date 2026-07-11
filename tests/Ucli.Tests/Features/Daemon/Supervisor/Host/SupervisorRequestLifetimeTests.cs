using MackySoft.Tests;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorRequestLifetimeTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task CancelForResponseStreamFailure_WhenRequestCancellationCallbackBlocks_ReturnsWithoutWaitingForCallback ()
    {
        await using var stream = new SupervisorControlledReadStream(
            SupervisorControlledReadMode.AsynchronousCancellationAware);
        var requestLifetime = SupervisorRequestLifetime.Start(stream, CancellationToken.None);
        await TestAwaiter.WaitAsync(
            stream.ReadStarted,
            "Supervisor disconnect monitor read",
            SignalWaitTimeout);
        using var releaseCancellationCallback = new ManualResetEventSlim();
        var cancellationCallbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = requestLifetime.CancellationToken.Register(() =>
        {
            cancellationCallbackEntered.TrySetResult();
            releaseCancellationCallback.Wait();
        });

        var cancelInvocationTask = Task.Run(requestLifetime.CancelForResponseStreamFailure);
        await TestAwaiter.WaitAsync(
            cancellationCallbackEntered.Task,
            "Supervisor request cancellation callback entry",
            SignalWaitTimeout);

        var returnedWithoutWaiting = false;
        try
        {
            await cancelInvocationTask.WaitAsync(SignalWaitTimeout);
            await TestAwaiter.WaitAsync(
                stream.ReadReturned,
                "Supervisor disconnect monitor read return",
                SignalWaitTimeout);
            returnedWithoutWaiting = true;
        }
        finally
        {
            releaseCancellationCallback.Set();
            stream.CompleteRead();
            await cancelInvocationTask.WaitAsync(SignalWaitTimeout);
            requestLifetime.Release();
        }

        Assert.True(returnedWithoutWaiting);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Release_WhenDisconnectMonitorIgnoresCancellation_ReturnsWithoutWaitingForMonitor ()
    {
        await using var stream = new SupervisorControlledReadStream(
            SupervisorControlledReadMode.AsynchronousIgnoringCancellation);
        var requestLifetime = SupervisorRequestLifetime.Start(stream, CancellationToken.None);
        await TestAwaiter.WaitAsync(
            stream.ReadStarted,
            "Supervisor disconnect monitor read",
            SignalWaitTimeout);

        var releaseTask = Task.Run(requestLifetime.Release);
        var returnedWithoutWaiting = false;
        try
        {
            await releaseTask.WaitAsync(SignalWaitTimeout);
            returnedWithoutWaiting = true;
        }
        finally
        {
            stream.CompleteRead();
            await TestAwaiter.WaitAsync(
                stream.ReadReturned,
                "Supervisor disconnect monitor read return",
                SignalWaitTimeout);
            await releaseTask.WaitAsync(SignalWaitTimeout);
        }

        Assert.True(returnedWithoutWaiting);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenDisconnectMonitorReadBlocksBeforeReturningValueTask_ReturnsWithoutWaitingForRead ()
    {
        await using var stream = new SupervisorControlledReadStream(
            SupervisorControlledReadMode.SynchronousBeforeValueTaskReturn);
        var startTask = Task.Run(() => SupervisorRequestLifetime.Start(stream, CancellationToken.None));
        await TestAwaiter.WaitAsync(
            stream.ReadStarted,
            "Synchronous supervisor disconnect monitor read",
            SignalWaitTimeout);

        SupervisorRequestLifetime? requestLifetime = null;
        var returnedWithoutWaiting = false;
        try
        {
            requestLifetime = await startTask.WaitAsync(SignalWaitTimeout);
            returnedWithoutWaiting = true;
        }
        finally
        {
            stream.CompleteRead();
            await TestAwaiter.WaitAsync(
                stream.ReadReturned,
                "Synchronous supervisor disconnect monitor read return",
                SignalWaitTimeout);
            requestLifetime ??= await startTask.WaitAsync(SignalWaitTimeout);
            requestLifetime.Release();
        }

        Assert.True(returnedWithoutWaiting);
    }
}
