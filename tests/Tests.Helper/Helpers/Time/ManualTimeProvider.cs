namespace MackySoft.Tests;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object syncObject = new();

    private readonly DateTimeOffset startUtc;

    private readonly List<ManualTimer> timers = [];

    private long currentTimestamp;

    internal ManualTimeProvider (DateTimeOffset? startUtc = null)
    {
        this.startUtc = startUtc ?? DateTimeOffset.UnixEpoch;
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow ()
    {
        return startUtc + TimeSpan.FromTicks(currentTimestamp);
    }

    public override long GetTimestamp ()
    {
        return currentTimestamp;
    }

    internal int ActiveTimerCount
    {
        get
        {
            lock (syncObject)
            {
                return timers.Count(static timer => !timer.IsDisposed);
            }
        }
    }

    public override ITimer CreateTimer (
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (syncObject)
        {
            timers.Add(timer);
        }

        return timer;
    }

    internal void Advance (TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

        List<(TimerCallback Callback, object? State)> pendingCallbacks = [];
        lock (syncObject)
        {
            currentTimestamp += elapsed.Ticks;
            CollectDueCallbacks(pendingCallbacks);
        }

        while (pendingCallbacks.Count > 0)
        {
            foreach (var pendingCallback in pendingCallbacks)
            {
                pendingCallback.Callback(pendingCallback.State);
            }

            pendingCallbacks.Clear();
            lock (syncObject)
            {
                CollectDueCallbacks(pendingCallbacks);
            }
        }
    }

    private void CollectDueCallbacks (List<(TimerCallback Callback, object? State)> pendingCallbacks)
    {
        for (var i = timers.Count - 1; i >= 0; i--)
        {
            var timer = timers[i];
            if (timer.IsDisposed)
            {
                timers.RemoveAt(i);
                continue;
            }

            while (timer.TryDequeueDueCallback(currentTimestamp, out var callback, out var state))
            {
                pendingCallbacks.Add((callback, state));
            }
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider owner;

        private readonly TimerCallback callback;

        private readonly object? state;

        private long? dueTimestamp;

        private long? periodTicks;

        public ManualTimer (
            ManualTimeProvider owner,
            TimerCallback callback,
            object? state)
        {
            this.owner = owner;
            this.callback = callback;
            this.state = state;
        }

        public bool IsDisposed { get; private set; }

        public bool Change (
            TimeSpan dueTime,
            TimeSpan period)
        {
            lock (owner.syncObject)
            {
                ThrowIfDisposed();
                dueTimestamp = ToScheduledTimestamp(dueTime);
                periodTicks = ToPeriodTicks(period);
                return true;
            }
        }

        public void Dispose ()
        {
            lock (owner.syncObject)
            {
                IsDisposed = true;
                dueTimestamp = null;
                periodTicks = null;
            }
        }

        public ValueTask DisposeAsync ()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public bool TryDequeueDueCallback (
            long currentTimestamp,
            out TimerCallback callback,
            out object? state)
        {
            if (IsDisposed
                || dueTimestamp is not long scheduledTimestamp
                || scheduledTimestamp > currentTimestamp)
            {
                callback = null!;
                state = null;
                return false;
            }

            callback = this.callback;
            state = this.state;
            if (periodTicks is long period)
            {
                dueTimestamp = scheduledTimestamp + period;
            }
            else
            {
                dueTimestamp = null;
            }

            return true;
        }

        private long? ToScheduledTimestamp (TimeSpan dueTime)
        {
            if (dueTime == Timeout.InfiniteTimeSpan)
            {
                return null;
            }

            ArgumentOutOfRangeException.ThrowIfLessThan(dueTime, TimeSpan.Zero);
            return owner.currentTimestamp + dueTime.Ticks;
        }

        private static long? ToPeriodTicks (TimeSpan period)
        {
            if (period == Timeout.InfiniteTimeSpan)
            {
                return null;
            }

            ArgumentOutOfRangeException.ThrowIfLessThan(period, TimeSpan.Zero);
            return period.Ticks;
        }

        private void ThrowIfDisposed ()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ManualTimer));
            }
        }
    }
}
