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
}
