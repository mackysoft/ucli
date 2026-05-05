using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;

namespace MackySoft.Ucli.Application.Tests.Execution.Timeout;

public sealed class ExecutionDeadlineTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Start_WithPositiveTimeout_TryGetRemainingTimeoutReturnsTrue ()
    {
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);

        var result = deadline.TryGetRemainingTimeout(out var remainingTimeout);

        Assert.True(result);
        Assert.True(remainingTimeout > TimeSpan.Zero);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Start_WhenTimeoutElapsed_TryGetRemainingTimeoutReturnsFalse ()
    {
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10), timeProvider);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

        var result = deadline.TryGetRemainingTimeout(out var remainingTimeout);

        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, remainingTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsExpired_BeforeTimeout_ReturnsFalse ()
    {
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);

        Assert.False(deadline.IsExpired);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsExpired_AfterTimeout_ReturnsTrue ()
    {
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10), timeProvider);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

        Assert.True(deadline.IsExpired);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetRemainingWaitMilliseconds_WhenTimeoutElapsed_ReturnsZero ()
    {
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10), timeProvider);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

        var remainingMilliseconds = deadline.GetRemainingWaitMilliseconds();

        Assert.Equal(0, remainingMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetRemainingWaitMilliseconds_BeforeTimeout_ReturnsTrueWithPositiveMilliseconds ()
    {
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);

        var result = deadline.TryGetRemainingWaitMilliseconds(out var remainingMilliseconds);

        Assert.True(result);
        Assert.True(remainingMilliseconds > 0);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetRemainingWaitMilliseconds_WhenTimeoutElapsed_ReturnsFalseWithZero ()
    {
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10), timeProvider);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

        var result = deadline.TryGetRemainingWaitMilliseconds(out var remainingMilliseconds);

        Assert.False(result);
        Assert.Equal(0, remainingMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Start_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = ExecutionDeadline.Start(TimeSpan.Zero);
        });
    }
}
