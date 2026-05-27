namespace MackySoft.Tests;

public sealed class ManualTimeProviderTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForTimerDueWithinAsync_WhenFiniteTimerIsInsideWindow_Completes ()
    {
        var timeProvider = new ManualTimeProvider();
        var waitTask = timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(10));

        using var timer = timeProvider.CreateTimer(
            static _ => { },
            null,
            TimeSpan.FromMilliseconds(10),
            Timeout.InfiniteTimeSpan);

        await TestAwaiter.WaitAsync(waitTask, "manual finite timer registration", SignalWaitTimeout);

        Assert.Equal(1, timeProvider.ActiveTimerCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForTimerDueWithinAsync_WhenInfiniteTimerIsActivatedInsideWindow_CompletesAfterActivation ()
    {
        var timeProvider = new ManualTimeProvider();
        var waitTask = timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(10));
        using var timer = timeProvider.CreateTimer(
            static _ => { },
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

        Assert.False(waitTask.IsCompleted);
        Assert.Equal(0, timeProvider.ActiveTimerCount);

        timer.Change(TimeSpan.FromMilliseconds(10), Timeout.InfiniteTimeSpan);
        await TestAwaiter.WaitAsync(waitTask, "manual activated timer registration", SignalWaitTimeout);

        Assert.Equal(1, timeProvider.ActiveTimerCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForTimerDueWithinAsync_WhenOnlyFarTimerExists_CompletesAfterNearTimerIsCreated ()
    {
        var timeProvider = new ManualTimeProvider();
        using var farTimer = timeProvider.CreateTimer(
            static _ => { },
            null,
            TimeSpan.FromSeconds(30),
            Timeout.InfiniteTimeSpan);
        var waitTask = timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(100));

        Assert.False(waitTask.IsCompleted);

        using var nearTimer = timeProvider.CreateTimer(
            static _ => { },
            null,
            TimeSpan.FromMilliseconds(100),
            Timeout.InfiniteTimeSpan);
        await TestAwaiter.WaitAsync(waitTask, "manual near timer registration", SignalWaitTimeout);

        Assert.True(timeProvider.TryGetNextTimerDelay(out var nextTimerDelay));
        Assert.Equal(TimeSpan.FromMilliseconds(100), nextTimerDelay);
    }
}
