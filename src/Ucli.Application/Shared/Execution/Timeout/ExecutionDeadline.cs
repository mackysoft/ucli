namespace MackySoft.Ucli.Application.Shared.Execution.Timeout;

/// <summary> Represents one execution deadline and exposes remaining-time queries. </summary>
internal sealed class ExecutionDeadline
{
    private readonly TimeProvider timeProvider;

    private readonly long startTimestamp;

    private ExecutionDeadline (
        TimeProvider timeProvider,
        long startTimestamp,
        TimeSpan timeout,
        DateTimeOffset utcDeadline)
    {
        this.timeProvider = timeProvider;
        this.startTimestamp = startTimestamp;
        Timeout = timeout;
        UtcDeadline = utcDeadline;
    }

    /// <summary> Creates one deadline from the specified timeout budget. </summary>
    /// <param name="timeout"> The timeout budget. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="timeProvider"> The time provider used for monotonic elapsed-time measurements. </param>
    /// <returns> The created execution deadline value. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="timeProvider" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public static ExecutionDeadline Start (
        TimeSpan timeout,
        TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var startTimestamp = timeProvider.GetTimestamp();
        return new ExecutionDeadline(
            timeProvider,
            startTimestamp,
            timeout,
            AddUtcClamped(timeProvider.GetUtcNow(), timeout));
    }

    /// <summary> Creates a deadline from one previously captured monotonic and UTC admission observation. </summary>
    /// <param name="timeout"> The positive timeout derived from the admission observation. </param>
    /// <param name="observedAtUtc"> The UTC instant captured for the admission observation. </param>
    /// <param name="startTimestamp"> The monotonic timestamp captured for the admission observation. </param>
    /// <param name="timeProvider"> The time provider that produced <paramref name="startTimestamp" />. </param>
    /// <returns> The created execution deadline. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is not positive. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="timeProvider" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="observedAtUtc" /> is default or is not expressed in UTC. </exception>
    public static ExecutionDeadline StartFromObservation (
        TimeSpan timeout,
        DateTimeOffset observedAtUtc,
        long startTimestamp,
        TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (observedAtUtc == default || observedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("UTC observation must be a non-default UTC value.", nameof(observedAtUtc));
        }

        return new ExecutionDeadline(
            timeProvider,
            startTimestamp,
            timeout,
            AddUtcClamped(observedAtUtc, timeout));
    }

    /// <summary> Gets whether the execution deadline has already elapsed. </summary>
    public bool IsExpired => !TryGetRemainingTimeout(out _);

    /// <summary> Gets the UTC deadline captured when this execution deadline was created. </summary>
    public DateTimeOffset UtcDeadline { get; }

    /// <summary> Gets the original timeout used to create this deadline. </summary>
    public TimeSpan Timeout { get; }

    /// <summary> Gets the time provider that owns this deadline. </summary>
    internal TimeProvider Clock => timeProvider;

    /// <summary> Creates a completion deadline that shares this deadline's monotonic start point. </summary>
    /// <param name="completionGrace"> The additional time reserved for completion work after the execution timeout. </param>
    /// <returns> A deadline whose timeout is this execution timeout plus <paramref name="completionGrace" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="completionGrace" /> is not positive. </exception>
    public ExecutionDeadline CreateCompletionDeadline (TimeSpan completionGrace)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(completionGrace, TimeSpan.Zero);

        return new ExecutionDeadline(
            timeProvider,
            startTimestamp,
            AddDurationClamped(Timeout, completionGrace),
            AddUtcClamped(UtcDeadline, completionGrace));
    }

    /// <summary> Creates a shorter deadline that cannot outlive this deadline. </summary>
    /// <param name="maximumDuration"> The maximum duration allowed from the current monotonic observation. </param>
    /// <returns> A deadline capped by both <paramref name="maximumDuration" /> and this deadline. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="maximumDuration" /> is not positive. </exception>
    public ExecutionDeadline CreateCappedDeadline (TimeSpan maximumDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumDuration, TimeSpan.Zero);

        var elapsed = timeProvider.GetElapsedTime(startTimestamp);
        var cappedTimeout = AddDurationClamped(elapsed, maximumDuration);
        if (cappedTimeout > Timeout)
        {
            cappedTimeout = Timeout;
        }

        var cappedUtcDeadline = SubtractUtcClamped(UtcDeadline, Timeout - cappedTimeout);

        return new ExecutionDeadline(
            timeProvider,
            startTimestamp,
            cappedTimeout,
            cappedUtcDeadline);
    }

    /// <summary> Tries to get remaining timeout budget from monotonic elapsed time. </summary>
    /// <param name="remainingTimeout"> The remaining timeout when available; otherwise <see cref="TimeSpan.Zero" />. </param>
    /// <returns> <see langword="true" /> when remaining timeout is positive; otherwise <see langword="false" />. </returns>
    public bool TryGetRemainingTimeout (out TimeSpan remainingTimeout)
    {
        var elapsed = timeProvider.GetElapsedTime(startTimestamp);
        remainingTimeout = Timeout - elapsed;
        if (remainingTimeout <= TimeSpan.Zero)
        {
            remainingTimeout = TimeSpan.Zero;
            return false;
        }

        return true;
    }

    /// <summary> Tries to get the remaining timeout rounded up to milliseconds. </summary>
    /// <param name="remainingMilliseconds"> The positive remaining timeout in milliseconds when available; otherwise <c>0</c>. </param>
    /// <returns> <see langword="true" /> when remaining timeout is positive; otherwise <see langword="false" />. </returns>
    public bool TryGetRemainingMilliseconds (out int remainingMilliseconds)
    {
        if (!TryGetRemainingTimeout(out var remainingTimeout))
        {
            remainingMilliseconds = 0;
            return false;
        }

        var roundedMilliseconds = Math.Ceiling(remainingTimeout.TotalMilliseconds);
        remainingMilliseconds = roundedMilliseconds >= int.MaxValue
            ? int.MaxValue
            : (int)roundedMilliseconds;
        return true;
    }

    private static DateTimeOffset AddUtcClamped (
        DateTimeOffset value,
        TimeSpan duration)
    {
        return DateTimeOffset.MaxValue - value < duration
            ? DateTimeOffset.MaxValue
            : value + duration;
    }

    private static DateTimeOffset SubtractUtcClamped (
        DateTimeOffset value,
        TimeSpan duration)
    {
        return value - DateTimeOffset.MinValue < duration
            ? DateTimeOffset.MinValue
            : value - duration;
    }

    private static TimeSpan AddDurationClamped (
        TimeSpan value,
        TimeSpan duration)
    {
        return TimeSpan.MaxValue - value < duration
            ? TimeSpan.MaxValue
            : value + duration;
    }
}
