namespace MackySoft.Tests;

/// <summary> Drives asynchronous test work that is blocked on <see cref="ManualTimeProvider" /> timers. </summary>
internal static class ManualTimeTaskDriver
{
    internal static async ValueTask AdvanceUntilCompletedAsync (
        ManualTimeProvider timeProvider,
        Task observedTask,
        TimeSpan totalTime,
        TimeSpan maximumTimerDelay)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(observedTask);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalTime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumTimerDelay, TimeSpan.Zero);

        var elapsed = TimeSpan.Zero;
        while (!observedTask.IsCompleted)
        {
            await WaitForTimerDueWithinOrCompletionAsync(timeProvider, observedTask, maximumTimerDelay).ConfigureAwait(false);
            if (observedTask.IsCompleted)
            {
                return;
            }

            if (!timeProvider.TryGetNextTimerDelay(out var nextTimerDelay) || nextTimerDelay > maximumTimerDelay)
            {
                continue;
            }

            var remainingTime = totalTime - elapsed;
            if (remainingTime <= TimeSpan.Zero && nextTimerDelay > TimeSpan.Zero)
            {
                throw new TimeoutException($"The observed task did not complete within {totalTime} of manual time.");
            }

            var advanceBy = nextTimerDelay < remainingTime ? nextTimerDelay : remainingTime;
            timeProvider.Advance(advanceBy);
            elapsed += advanceBy;
        }
    }

    internal static async ValueTask WaitForTimerDueWithinOrCompletionAsync (
        ManualTimeProvider timeProvider,
        Task observedTask,
        TimeSpan maximumDelay)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(observedTask);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDelay, TimeSpan.Zero);

        if (observedTask.IsCompleted
            || (timeProvider.TryGetNextTimerDelay(out var nextTimerDelay) && nextTimerDelay <= maximumDelay))
        {
            return;
        }

        var timerTask = timeProvider.WaitForTimerDueWithinAsync(maximumDelay);
        if (observedTask.IsCompleted || timerTask.IsCompleted)
        {
            return;
        }

        await Task.WhenAny(observedTask, timerTask).ConfigureAwait(false);
    }
}
