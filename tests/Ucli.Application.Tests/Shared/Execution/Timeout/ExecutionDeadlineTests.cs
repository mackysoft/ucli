namespace MackySoft.Ucli.Application.Tests.Execution.Timeout;

public sealed class ExecutionDeadlineTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Start_WithPositiveTimeout_TryGetRemainingTimeoutReturnsTrue ()
    {
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);

        var result = deadline.TryGetRemainingTimeout(out var remainingTimeout);

        Assert.True(result);
        Assert.True(remainingTimeout > TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromSeconds(1), deadline.Timeout);
        Assert.Equal(startedAtUtc + TimeSpan.FromSeconds(1), deadline.UtcDeadline);
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
    public void Start_WhenUtcDeadlineExceedsMaximum_ClampsUtcDeadline ()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.MaxValue - TimeSpan.FromSeconds(1));

        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(2), timeProvider);

        Assert.Equal(DateTimeOffset.MaxValue, deadline.UtcDeadline);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateCompletionDeadline_AfterElapsedTime_PreservesOriginalMonotonicStartPoint ()
    {
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(700), timeProvider);
        timeProvider.Advance(TimeSpan.FromMilliseconds(400));

        var completionDeadline = deadline.CreateCompletionDeadline(TimeSpan.FromSeconds(1));

        Assert.True(completionDeadline.TryGetRemainingTimeout(out var remainingTimeout));
        Assert.Equal(TimeSpan.FromMilliseconds(1300), remainingTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateCompletionDeadline_WhenUtcDeadlineCannotBeExtended_ClampsUtcDeadline ()
    {
        var deadline = ExecutionDeadline.StartFromObservation(
            TimeSpan.FromSeconds(1),
            DateTimeOffset.MaxValue - TimeSpan.FromMilliseconds(500),
            TimeProvider.System.GetTimestamp(),
            TimeProvider.System);

        var completionDeadline = deadline.CreateCompletionDeadline(TimeSpan.FromSeconds(1));

        Assert.Equal(DateTimeOffset.MaxValue, completionDeadline.UtcDeadline);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateCompletionDeadline_WhenTimeoutCannotBeExtended_ClampsTimeout ()
    {
        var deadline = ExecutionDeadline.StartFromObservation(
            TimeSpan.MaxValue,
            DateTimeOffset.UnixEpoch,
            TimeProvider.System.GetTimestamp(),
            TimeProvider.System);

        var completionDeadline = deadline.CreateCompletionDeadline(TimeSpan.FromTicks(1));

        Assert.Equal(TimeSpan.MaxValue, completionDeadline.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateCappedDeadline_AfterElapsedTime_ExpiresAtMaximumDurationFromObservation ()
    {
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);
        timeProvider.Advance(TimeSpan.FromMilliseconds(400));

        var cappedDeadline = deadline.CreateCappedDeadline(TimeSpan.FromMilliseconds(200));

        Assert.True(cappedDeadline.TryGetRemainingTimeout(out var remainingTimeout));
        Assert.Equal(TimeSpan.FromMilliseconds(200), remainingTimeout);
        Assert.Equal(startedAtUtc + TimeSpan.FromMilliseconds(600), cappedDeadline.UtcDeadline);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateCappedDeadline_WhenParentExpiresSooner_DoesNotOutliveParent ()
    {
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);
        timeProvider.Advance(TimeSpan.FromMilliseconds(400));

        var cappedDeadline = deadline.CreateCappedDeadline(TimeSpan.FromMilliseconds(800));

        Assert.True(cappedDeadline.TryGetRemainingTimeout(out var remainingTimeout));
        Assert.Equal(TimeSpan.FromMilliseconds(600), remainingTimeout);
        Assert.Equal(deadline.UtcDeadline, cappedDeadline.UtcDeadline);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateCappedDeadline_AfterUtcClockShift_PreservesParentMonotonicToUtcMapping ()
    {
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);
        timeProvider.Advance(TimeSpan.FromMilliseconds(400));
        timeProvider.ShiftUtc(TimeSpan.FromDays(-1));

        var cappedDeadline = deadline.CreateCappedDeadline(TimeSpan.FromMilliseconds(200));

        Assert.True(cappedDeadline.TryGetRemainingTimeout(out var remainingTimeout));
        Assert.Equal(TimeSpan.FromMilliseconds(200), remainingTimeout);
        Assert.Equal(startedAtUtc + TimeSpan.FromMilliseconds(600), cappedDeadline.UtcDeadline);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void StartFromObservation_PreservesCapturedMonotonicAndUtcObservation ()
    {
        var timeProvider = new ManualTimeProvider();
        var startTimestamp = timeProvider.GetTimestamp();
        var observedAtUtc = timeProvider.GetUtcNow();
        timeProvider.Advance(TimeSpan.FromMilliseconds(400));
        timeProvider.ShiftUtc(TimeSpan.FromDays(-1));

        var deadline = ExecutionDeadline.StartFromObservation(
            TimeSpan.FromMilliseconds(700),
            observedAtUtc,
            startTimestamp,
            timeProvider);

        Assert.Equal(observedAtUtc + TimeSpan.FromMilliseconds(700), deadline.UtcDeadline);
        Assert.True(deadline.TryGetRemainingTimeout(out var remainingTimeout));
        Assert.Equal(TimeSpan.FromMilliseconds(300), remainingTimeout);
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
    public void TryGetRemainingMilliseconds_BeforeTimeout_ReturnsTrueWithPositiveMilliseconds ()
    {
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);

        var result = deadline.TryGetRemainingMilliseconds(out var remainingMilliseconds);

        Assert.True(result);
        Assert.True(remainingMilliseconds > 0);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetRemainingMilliseconds_WhenRemainingTimeoutIsSubMillisecond_RoundsUpToOne ()
    {
        var deadline = ExecutionDeadline.Start(TimeSpan.FromTicks(1), new ManualTimeProvider());

        var result = deadline.TryGetRemainingMilliseconds(out var remainingMilliseconds);

        Assert.True(result);
        Assert.Equal(1, remainingMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetRemainingMilliseconds_WhenRemainingTimeoutExceedsInt32Milliseconds_ClampsToInt32Maximum ()
    {
        var deadline = ExecutionDeadline.Start(TimeSpan.FromDays(30), new ManualTimeProvider());

        var result = deadline.TryGetRemainingMilliseconds(out var remainingMilliseconds);

        Assert.True(result);
        Assert.Equal(int.MaxValue, remainingMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetRemainingMilliseconds_WhenTimeoutElapsed_ReturnsFalseWithZero ()
    {
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10), timeProvider);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

        var result = deadline.TryGetRemainingMilliseconds(out var remainingMilliseconds);

        Assert.False(result);
        Assert.Equal(0, remainingMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Start_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = ExecutionDeadline.Start(TimeSpan.Zero, TimeProvider.System);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Start_WhenTimeProviderIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), null!);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateCompletionDeadline_WithNonPositiveGrace_ThrowsArgumentOutOfRangeException ()
    {
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = deadline.CreateCompletionDeadline(TimeSpan.Zero);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateCappedDeadline_WithNonPositiveDuration_ThrowsArgumentOutOfRangeException ()
    {
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = deadline.CreateCappedDeadline(TimeSpan.Zero);
        });
    }

    [Theory]
    [MemberData(nameof(InvalidUtcDeadlines))]
    [Trait("Size", "Small")]
    public void StartFromObservation_WithInvalidUtcObservation_ThrowsArgumentException (DateTimeOffset observedAtUtc)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = ExecutionDeadline.StartFromObservation(
                TimeSpan.FromSeconds(1),
                observedAtUtc,
                startTimestamp: 0,
                TimeProvider.System);
        });
    }

    public static TheoryData<DateTimeOffset> InvalidUtcDeadlines => new()
    {
        default,
        new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.FromHours(9)),
    };
}
