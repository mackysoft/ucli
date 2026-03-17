namespace MackySoft.Ucli.Execution;

/// <summary> Provides timer-backed delay primitives bound to one <see cref="TimeProvider" />. </summary>
internal static class TimeProviderDelay
{
    /// <summary> Asynchronously waits for the specified delay by using the supplied <see cref="TimeProvider" />. </summary>
    /// <param name="delay"> The delay to wait. Must be greater than or equal to <see cref="TimeSpan.Zero" />. </param>
    /// <param name="timeProvider"> The time provider that owns the timer. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> A task that completes when the delay elapses or cancellation is requested. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="timeProvider" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="delay" /> is less than <see cref="TimeSpan.Zero" />. </exception>
    public static Task Delay (
        TimeSpan delay,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        if (delay == TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        var state = new DelayState(cancellationToken);
        state.Initialize(timeProvider, delay);
        return state.Task;
    }

    private sealed class DelayState
    {
        private readonly CancellationToken cancellationToken;

        private readonly TaskCompletionSource taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private CancellationTokenRegistration cancellationRegistration;

        private ITimer? timer;

        private int completionState;

        public DelayState (CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }

        public Task Task => taskCompletionSource.Task;

        public void Initialize (
            TimeProvider timeProvider,
            TimeSpan delay)
        {
            timer = timeProvider.CreateTimer(
                static state => ((DelayState)state!).Complete(),
                this,
                delay,
                Timeout.InfiniteTimeSpan);

            if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(
                    static state => ((DelayState)state!).Cancel(),
                    this);
            }
        }

        private void Complete ()
        {
            if (Interlocked.CompareExchange(ref completionState, 1, 0) != 0)
            {
                return;
            }

            DisposeResources();
            taskCompletionSource.TrySetResult();
        }

        private void Cancel ()
        {
            if (Interlocked.CompareExchange(ref completionState, 1, 0) != 0)
            {
                return;
            }

            DisposeResources();
            taskCompletionSource.TrySetCanceled(cancellationToken);
        }

        private void DisposeResources ()
        {
            timer?.Dispose();
            timer = null;
            cancellationRegistration.Dispose();
        }
    }
}