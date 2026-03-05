namespace MackySoft.Ucli.Execution;

/// <summary> Represents one execution deadline and exposes remaining-time queries. </summary>
internal readonly struct ExecutionDeadline
{
    private readonly DateTimeOffset deadlineUtc;

    private ExecutionDeadline (DateTimeOffset deadlineUtc)
    {
        this.deadlineUtc = deadlineUtc;
    }

    /// <summary> Creates one deadline from the specified timeout budget. </summary>
    /// <param name="timeout"> The timeout budget. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <returns> The created execution deadline value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public static ExecutionDeadline Start (TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        return new ExecutionDeadline(DateTimeOffset.UtcNow + timeout);
    }

    /// <summary> Gets whether the execution deadline has already elapsed. </summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= deadlineUtc;

    /// <summary> Tries to get remaining timeout budget from the current UTC clock. </summary>
    /// <param name="remainingTimeout"> The remaining timeout when available; otherwise <see cref="TimeSpan.Zero" />. </param>
    /// <returns> <see langword="true" /> when remaining timeout is positive; otherwise <see langword="false" />. </returns>
    public bool TryGetRemainingTimeout (out TimeSpan remainingTimeout)
    {
        if (IsExpired)
        {
            remainingTimeout = TimeSpan.Zero;
            return false;
        }

        remainingTimeout = deadlineUtc - DateTimeOffset.UtcNow;
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