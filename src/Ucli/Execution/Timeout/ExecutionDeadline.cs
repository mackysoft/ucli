namespace MackySoft.Ucli.Execution;

/// <summary> Represents one execution deadline and exposes remaining-time queries. </summary>
internal readonly struct ExecutionDeadline
{
    private readonly TimeProvider timeProvider;

    private readonly long startTimestamp;

    private readonly TimeSpan timeout;

    private ExecutionDeadline (
        TimeProvider timeProvider,
        long startTimestamp,
        TimeSpan timeout)
    {
        this.timeProvider = timeProvider;
        this.startTimestamp = startTimestamp;
        this.timeout = timeout;
    }

    /// <summary> Creates one deadline from the specified timeout budget. </summary>
    /// <param name="timeout"> The timeout budget. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="timeProvider"> The time provider used for monotonic elapsed-time measurements. </param>
    /// <returns> The created execution deadline value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public static ExecutionDeadline Start (
        TimeSpan timeout,
        TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        timeProvider ??= TimeProvider.System;

        return new ExecutionDeadline(
            timeProvider,
            timeProvider.GetTimestamp(),
            timeout);
    }

    /// <summary> Gets whether the execution deadline has already elapsed. </summary>
    public bool IsExpired => !TryGetRemainingTimeout(out _);

    /// <summary> Tries to get remaining timeout budget from monotonic elapsed time. </summary>
    /// <param name="remainingTimeout"> The remaining timeout when available; otherwise <see cref="TimeSpan.Zero" />. </param>
    /// <returns> <see langword="true" /> when remaining timeout is positive; otherwise <see langword="false" />. </returns>
    public bool TryGetRemainingTimeout (out TimeSpan remainingTimeout)
    {
        var elapsed = timeProvider.GetElapsedTime(startTimestamp);
        remainingTimeout = timeout - elapsed;
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
}