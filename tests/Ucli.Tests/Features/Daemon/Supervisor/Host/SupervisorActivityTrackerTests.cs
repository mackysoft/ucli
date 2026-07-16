namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorActivityTrackerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsIdle_WhenMonotonicClockReachesThreshold_ReturnsTrue ()
    {
        var timeProvider = new ManualTimeProvider();
        var tracker = new SupervisorActivityTracker(timeProvider);
        var idleDelay = TimeSpan.FromSeconds(10);

        Assert.False(tracker.IsIdle(idleDelay));

        timeProvider.Advance(idleDelay);

        Assert.True(tracker.IsIdle(idleDelay));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsIdle_WhenLongRunningRequestEnds_StartsIdlePeriodAtRequestCompletion ()
    {
        var timeProvider = new ManualTimeProvider();
        var tracker = new SupervisorActivityTracker(timeProvider);
        var idleDelay = TimeSpan.FromSeconds(10);
        var request = tracker.BeginRequest();

        Assert.True(tracker.HasActiveRequests);

        timeProvider.Advance(idleDelay);

        request.Dispose();

        Assert.False(tracker.HasActiveRequests);
        Assert.False(tracker.IsIdle(idleDelay));

        timeProvider.Advance(idleDelay);

        Assert.True(tracker.IsIdle(idleDelay));
    }
}
