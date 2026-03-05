using MackySoft.Ucli.Execution;

namespace MackySoft.Ucli.Tests.Execution.Timeout;

public sealed class ExecutionDeadlineTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Start_WithPositiveTimeout_TryGetRemainingTimeoutReturnsTrue ()
    {
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1));

        var result = deadline.TryGetRemainingTimeout(out var remainingTimeout);

        Assert.True(result);
        Assert.True(remainingTimeout > TimeSpan.Zero);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenTimeoutElapsed_TryGetRemainingTimeoutReturnsFalse ()
    {
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10));
        await Task.Delay(50, CancellationToken.None);

        var result = deadline.TryGetRemainingTimeout(out var remainingTimeout);

        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, remainingTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsExpired_BeforeTimeout_ReturnsFalse ()
    {
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1));

        Assert.False(deadline.IsExpired);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task IsExpired_AfterTimeout_ReturnsTrue ()
    {
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10));
        await Task.Delay(50, CancellationToken.None);

        Assert.True(deadline.IsExpired);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetRemainingWaitMilliseconds_WhenTimeoutElapsed_ReturnsZero ()
    {
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10));
        await Task.Delay(50, CancellationToken.None);

        var remainingMilliseconds = deadline.GetRemainingWaitMilliseconds();

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