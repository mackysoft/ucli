using System.Diagnostics;

namespace MackySoft.Ucli.Execution;

/// <summary> Represents one execution deadline and exposes remaining-time queries. </summary>
internal readonly struct ExecutionDeadline
{
    private readonly long deadlineTimestamp;

    private ExecutionDeadline (long deadlineTimestamp)
    {
        this.deadlineTimestamp = deadlineTimestamp;
    }

    /// <summary> Creates one deadline from the specified timeout budget. </summary>
    /// <param name="timeout"> The timeout budget. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <returns> The created execution deadline value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public static ExecutionDeadline Start (TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var startTimestamp = Stopwatch.GetTimestamp();
        var timeoutTicksDouble = timeout.TotalSeconds * Stopwatch.Frequency;
        var timeoutTimestampTicks = timeoutTicksDouble >= long.MaxValue
            ? long.MaxValue
            : Math.Max(1L, (long)Math.Ceiling(timeoutTicksDouble));
        var deadlineTimestamp = startTimestamp >= long.MaxValue - timeoutTimestampTicks
            ? long.MaxValue
            : startTimestamp + timeoutTimestampTicks;
        return new ExecutionDeadline(deadlineTimestamp);
    }

    /// <summary> Gets whether the execution deadline has already elapsed. </summary>
    public bool IsExpired => GetRemainingTimestampTicks() == 0;

    /// <summary> Tries to get remaining timeout budget from monotonic elapsed time. </summary>
    /// <param name="remainingTimeout"> The remaining timeout when available; otherwise <see cref="TimeSpan.Zero" />. </param>
    /// <returns> <see langword="true" /> when remaining timeout is positive; otherwise <see langword="false" />. </returns>
    public bool TryGetRemainingTimeout (out TimeSpan remainingTimeout)
    {
        var remainingTimestampTicks = GetRemainingTimestampTicks();
        if (remainingTimestampTicks == 0)
        {
            remainingTimeout = TimeSpan.Zero;
            return false;
        }

        remainingTimeout = TimeSpan.FromSeconds(remainingTimestampTicks / (double)Stopwatch.Frequency);
        if (remainingTimeout <= TimeSpan.Zero)
        {
            remainingTimeout = TimeSpan.Zero;
            return false;
        }

        return true;
    }

    /// <summary> Gets remaining timeout budget in milliseconds for wait APIs. </summary>
    /// <returns> Remaining wait budget in milliseconds. </returns>
    public int GetRemainingWaitMilliseconds ()
    {
        if (!TryGetRemainingTimeout(out var remainingTimeout))
        {
            return 0;
        }

        var remainingMilliseconds = Math.Ceiling(remainingTimeout.TotalMilliseconds);
        return remainingMilliseconds >= int.MaxValue
            ? int.MaxValue
            : (int)remainingMilliseconds;
    }

    /// <summary> Gets remaining timestamp ticks based on monotonic timer. </summary>
    /// <returns> Remaining timer ticks; returns <c>0</c> when expired. </returns>
    private long GetRemainingTimestampTicks ()
    {
        var remainingTimestampTicks = deadlineTimestamp - Stopwatch.GetTimestamp();
        return remainingTimestampTicks <= 0
            ? 0
            : remainingTimestampTicks;
    }
}