namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Tracks request activity and idle timing for one supervisor host instance. </summary>
internal sealed class SupervisorActivityTracker
{
    private readonly TimeProvider timeProvider;

    private int activeRequestCount;

    private long lastActivityTimestamp;

    /// <summary> Initializes a new instance of the <see cref="SupervisorActivityTracker" /> class. </summary>
    /// <param name="timeProvider"> The monotonic clock used for idle-duration measurement. </param>
    public SupervisorActivityTracker (TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        lastActivityTimestamp = timeProvider.GetTimestamp();
    }

    /// <summary> Gets a value indicating whether one supervisor request is currently being processed. </summary>
    public bool HasActiveRequests => Volatile.Read(ref activeRequestCount) > 0;

    /// <summary> Records one supervisor request scope and keeps the host active until disposal. </summary>
    /// <returns> One disposable request-activity scope. </returns>
    public IDisposable BeginRequest ()
    {
        Touch();
        Interlocked.Increment(ref activeRequestCount);
        return new RequestScope(this);
    }

    /// <summary> Updates the last observed activity timestamp by using the monotonic clock. </summary>
    public void Touch ()
    {
        Volatile.Write(ref lastActivityTimestamp, timeProvider.GetTimestamp());
    }

    /// <summary> Determines whether the host has been idle for the specified delay. </summary>
    /// <param name="idleDelay"> The idle-delay threshold. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <returns> <see langword="true" /> when the idle delay has elapsed without activity; otherwise <see langword="false" />. </returns>
    public bool IsIdle (TimeSpan idleDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleDelay, TimeSpan.Zero);

        return timeProvider.GetElapsedTime(
            Volatile.Read(ref lastActivityTimestamp),
            timeProvider.GetTimestamp()) >= idleDelay;
    }

    private void EndRequest ()
    {
        Touch();
        Interlocked.Decrement(ref activeRequestCount);
    }

    private sealed class RequestScope : IDisposable
    {
        private readonly SupervisorActivityTracker tracker;

        private int disposed;

        public RequestScope (SupervisorActivityTracker tracker)
        {
            this.tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        }

        public void Dispose ()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            tracker.EndRequest();
        }
    }
}
