namespace MackySoft.Ucli.Application.Shared.Execution.Timeout;

/// <summary> Tracks one timeout budget while allowing non-operation wait time to be excluded. </summary>
internal sealed class ExecutionTimeoutBudget
{
    private readonly TimeProvider timeProvider;

    private readonly long startTimestamp;

    private readonly TimeSpan timeout;

    private TimeSpan excludedElapsed;

    private ExecutionTimeoutBudget (
        TimeProvider timeProvider,
        long startTimestamp,
        TimeSpan timeout)
    {
        this.timeProvider = timeProvider;
        this.startTimestamp = startTimestamp;
        this.timeout = timeout;
    }

    /// <summary> Creates one timeout budget from the specified timeout value. </summary>
    /// <param name="timeout"> The timeout budget. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="timeProvider"> The time provider used for monotonic elapsed-time measurements. </param>
    /// <returns> The created timeout budget. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="timeProvider" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public static ExecutionTimeoutBudget Start (
        TimeSpan timeout,
        TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new ExecutionTimeoutBudget(
            timeProvider,
            timeProvider.GetTimestamp(),
            timeout);
    }

    /// <summary> Starts one section whose elapsed time is excluded from the budget. </summary>
    /// <returns> A scope that excludes elapsed time when disposed. </returns>
    public ExcludedSection BeginExcludedSection ()
    {
        return new ExcludedSection(this, timeProvider.GetTimestamp());
    }

    /// <summary> Tries to get remaining timeout budget from accounted elapsed time. </summary>
    /// <param name="remainingTimeout"> The remaining timeout when available; otherwise <see cref="TimeSpan.Zero" />. </param>
    /// <returns> <see langword="true" /> when remaining timeout is positive; otherwise <see langword="false" />. </returns>
    public bool TryGetRemainingTimeout (out TimeSpan remainingTimeout)
    {
        var elapsed = timeProvider.GetElapsedTime(startTimestamp) - excludedElapsed;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        remainingTimeout = timeout - elapsed;
        if (remainingTimeout <= TimeSpan.Zero)
        {
            remainingTimeout = TimeSpan.Zero;
            return false;
        }

        return true;
    }

    private void ExcludeElapsedSince (long excludedSectionStartTimestamp)
    {
        var elapsed = timeProvider.GetElapsedTime(excludedSectionStartTimestamp);
        if (elapsed > TimeSpan.Zero)
        {
            excludedElapsed += elapsed;
        }
    }

    /// <summary> Represents one timeout-budget exclusion scope. </summary>
    internal readonly struct ExcludedSection : IDisposable
    {
        private readonly ExecutionTimeoutBudget owner;

        private readonly long startTimestamp;

        internal ExcludedSection (
            ExecutionTimeoutBudget owner,
            long startTimestamp)
        {
            this.owner = owner;
            this.startTimestamp = startTimestamp;
        }

        /// <inheritdoc />
        public void Dispose ()
        {
            owner.ExcludeElapsedSince(startTimestamp);
        }
    }
}
