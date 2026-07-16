namespace MackySoft.Tests;

public sealed class ManualTimeTaskDriverTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task AdvanceUntilCompletedAsync_WhenFarTimeoutTimerExists_CompletesByAdvancingNearManualTimerOnly ()
    {
        var timeProvider = new ManualTimeProvider();
        var farDelayTask = Task.Delay(
            TimeSpan.FromSeconds(30),
            timeProvider,
            CancellationToken.None);
        var delayTask = Task.Delay(
            TimeSpan.FromMilliseconds(250),
            timeProvider,
            CancellationToken.None);

        var driveTask = ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
                timeProvider,
                delayTask,
                TimeSpan.FromMilliseconds(250),
                TimeSpan.FromMilliseconds(250))
            .AsTask();

        await TestAwaiter.WaitAsync(driveTask, "manual time task driver", SignalWaitTimeout);

        Assert.True(delayTask.IsCompletedSuccessfully);
        Assert.False(farDelayTask.IsCompleted);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + TimeSpan.FromMilliseconds(250),
            timeProvider.GetUtcNow());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AdvanceUntilCompletedAsync_WhenSignaledTimerIsDisposedBeforeInspection_WaitsForNextTimer ()
    {
        var timeProvider = new ManualTimeProvider();
        var observedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var driveTask = ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
                timeProvider,
                observedSource.Task,
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(100))
            .AsTask();
        Assert.False(driveTask.IsCompleted);

        var transientTimer = timeProvider.CreateTimer(
            _ => { },
            state: null,
            TimeSpan.FromMilliseconds(100),
            Timeout.InfiniteTimeSpan);
        transientTimer.Dispose();
        await Task.Yield();

        Assert.False(driveTask.IsCompleted);
        using var completionTimer = timeProvider.CreateTimer(
            _ => observedSource.SetResult(),
            state: null,
            TimeSpan.FromMilliseconds(100),
            Timeout.InfiniteTimeSpan);
        await TestAwaiter.WaitAsync(driveTask, "manual time task driver", SignalWaitTimeout);

        Assert.True(observedSource.Task.IsCompletedSuccessfully);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + TimeSpan.FromMilliseconds(100),
            timeProvider.GetUtcNow());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AdvanceUntilCompletedAsync_WhenTimerIsDueAtTimeLimit_ProcessesWithoutAdvancingPastLimit ()
    {
        var timeProvider = new ManualTimeProvider();
        var observedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var completionTimer = timeProvider.CreateTimer(
            _ => observedSource.SetResult(),
            state: null,
            TimeSpan.Zero,
            Timeout.InfiniteTimeSpan);

        var driveTask = ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
                timeProvider,
                observedSource.Task,
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(1))
            .AsTask();

        await TestAwaiter.WaitAsync(driveTask, "manual time boundary timer", SignalWaitTimeout);

        Assert.True(observedSource.Task.IsCompletedSuccessfully);
        Assert.Equal(DateTimeOffset.UnixEpoch, timeProvider.GetUtcNow());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AdvanceUntilCompletedAsync_WhenTimerIsAfterTimeLimit_ThrowsWithoutAdvancingPastLimit ()
    {
        var timeProvider = new ManualTimeProvider();
        var observedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var completionTimer = timeProvider.CreateTimer(
            _ => observedSource.SetResult(),
            state: null,
            TimeSpan.FromMilliseconds(2),
            Timeout.InfiniteTimeSpan);

        var driveTask = ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
                timeProvider,
                observedSource.Task,
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(2))
            .AsTask();

        await Assert.ThrowsAsync<TimeoutException>(() => driveTask);

        Assert.False(observedSource.Task.IsCompleted);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + TimeSpan.FromMilliseconds(1),
            timeProvider.GetUtcNow());
    }
}
