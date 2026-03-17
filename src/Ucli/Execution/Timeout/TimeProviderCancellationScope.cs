namespace MackySoft.Ucli.Execution;

/// <summary> Represents one linked cancellation scope driven by a timeout interpreted by a <see cref="TimeProvider" />. </summary>
internal sealed class TimeProviderCancellationScope : IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource;

    private ITimer? timer;

    private int timeoutTriggered;

    private bool disposed;

    private TimeProviderCancellationScope (
        CancellationTokenSource cancellationTokenSource,
        ITimer? timer)
    {
        this.cancellationTokenSource = cancellationTokenSource;
        this.timer = timer;
    }

    /// <summary> Gets the linked cancellation token. </summary>
    public CancellationToken Token => cancellationTokenSource.Token;

    /// <summary> Gets whether the timeout timer triggered cancellation. </summary>
    public bool HasTimedOut => Volatile.Read(ref timeoutTriggered) != 0;

    /// <summary> Creates one linked timeout scope. </summary>
    /// <param name="cancellationToken"> The outer cancellation token propagated by the caller. </param>
    /// <param name="timeout"> The timeout to apply. Must be greater than or equal to <see cref="TimeSpan.Zero" />. </param>
    /// <param name="timeProvider"> The time provider that interprets the timeout. </param>
    /// <returns> The created timeout scope. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="timeProvider" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than <see cref="TimeSpan.Zero" />. </exception>
    public static TimeProviderCancellationScope CreateLinked (
        CancellationToken cancellationToken,
        TimeSpan timeout,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);

        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout == TimeSpan.Zero)
        {
            cancellationTokenSource.Cancel();
            var zeroTimeoutScope = new TimeProviderCancellationScope(cancellationTokenSource, timer: null);
            zeroTimeoutScope.timeoutTriggered = 1;
            return zeroTimeoutScope;
        }

        var scope = new TimeProviderCancellationScope(cancellationTokenSource, timer: null);
        var timer = timeProvider.CreateTimer(
            static state => ((TimeProviderCancellationScope)state!).CancelForTimeout(),
            scope,
            timeout,
            Timeout.InfiniteTimeSpan);
        scope.timer = timer;
        return scope;
    }

    private void CancelForTimeout ()
    {
        if (disposed)
        {
            return;
        }

        Interlocked.Exchange(ref timeoutTriggered, 1);
        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose ()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        timer?.Dispose();
        cancellationTokenSource.Dispose();
    }
}