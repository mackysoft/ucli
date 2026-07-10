using System.Threading.Tasks.Sources;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Execution.Timeout;

public sealed class ExecutionDeadlineOperationTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenOperationBlocksBeforeReturningTask_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var operationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var allowOperationToReturn = new ManualResetEventSlim();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);

        var executionTask = Task.Run(async () => await ExecutionDeadlineOperation.ExecuteAsync(
            deadline,
            CancellationToken.None,
            "Deadline elapsed before operation.",
            "Deadline elapsed during operation.",
            _ =>
            {
                operationEntered.TrySetResult();
                allowOperationToReturn.Wait();
                return ValueTask.FromResult("late value");
            }));

        await operationEntered.Task;
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        try
        {
            var result = await executionTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error?.Kind);
        }
        finally
        {
            allowOperationToReturn.Set();
            await executionTask.WaitAsync(TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenOperationIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var operationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationCallbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationCallbackCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var allowCancellationCallbackCompletion = new ManualResetEventSlim();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);

        var executionTask = ExecutionDeadlineOperation.ExecuteAsync<string>(
                deadline,
                CancellationToken.None,
                "Deadline elapsed before operation.",
                "Deadline elapsed during operation.",
                operationCancellationToken =>
                {
                    _ = operationCancellationToken.Register(() =>
                    {
                        cancellationCallbackEntered.TrySetResult();
                        allowCancellationCallbackCompletion.Wait();
                        cancellationCallbackCompleted.TrySetResult();
                    });
                    operationEntered.TrySetResult();
                    return new ValueTask<string>(operationCompletion.Task);
                })
            .AsTask();

        await operationEntered.Task;
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        try
        {
            var result = await executionTask.WaitAsync(TimeSpan.FromSeconds(1));
            await cancellationCallbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error?.Kind);
        }
        finally
        {
            allowCancellationCallbackCompletion.Set();
            await cancellationCallbackCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            operationCompletion.TrySetException(new InvalidOperationException("Late read failure."));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenDeadlineWinsImmediatelyBeforeLateSuccess_ReturnsTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var operationSource = new InlineCompletingValueTaskSource<string>();
        var operationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);
        var executionTask = ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                CancellationToken.None,
                "Deadline elapsed before operation.",
                "Deadline elapsed during operation.",
                _ =>
                {
                    operationEntered.TrySetResult();
                    return operationSource.CreateValueTask();
                })
            .AsTask();
        await operationEntered.Task.WaitAsync(SignalWaitTimeout);
        await operationSource.ContinuationRegistered.WaitAsync(SignalWaitTimeout);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        operationSource.SetResult("late value");

        var result = await executionTask.WaitAsync(SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error?.Kind);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenDeadlineWinsImmediatelyBeforeLateFault_ReturnsTimeoutAndReleasesOperationCancellation ()
    {
        var timeProvider = new ManualTimeProvider();
        var operationSource = new InlineCompletingValueTaskSource<string>();
        var operationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);
        var operationCancellationToken = CancellationToken.None;
        var executionTask = ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                CancellationToken.None,
                "Deadline elapsed before operation.",
                "Deadline elapsed during operation.",
                cancellationToken =>
                {
                    operationCancellationToken = cancellationToken;
                    _ = cancellationToken.UnsafeRegister(
                        static state => ((TaskCompletionSource)state!).TrySetResult(),
                        operationCancellationObserved);
                    operationEntered.TrySetResult();
                    return operationSource.CreateValueTask();
                })
            .AsTask();
        await operationEntered.Task.WaitAsync(SignalWaitTimeout);
        await operationSource.ContinuationRegistered.WaitAsync(SignalWaitTimeout);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        operationSource.SetException(new InvalidOperationException("late fault"));

        var result = await executionTask.WaitAsync(SignalWaitTimeout);
        await operationCancellationObserved.Task.WaitAsync(SignalWaitTimeout);
        await WaitForCancellationTokenSourceDisposalAsync(operationCancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error?.Kind);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenOperationThreadExpiresDeadlineImmediatelyBeforeThrowing_ReturnsTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var operationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var startRace = new ManualResetEventSlim();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);
        var executionTask = ExecutionDeadlineOperation.ExecuteAsync<string>(
                deadline,
                CancellationToken.None,
                "Deadline elapsed before operation.",
                "Deadline elapsed during operation.",
                _ =>
                {
                    operationEntered.TrySetResult();
                    startRace.Wait();
                    timeProvider.Advance(TimeSpan.FromSeconds(1));
                    throw new InvalidOperationException("fault immediately after expiring deadline");
                })
            .AsTask();
        await operationEntered.Task.WaitAsync(SignalWaitTimeout);

        startRace.Set();

        var result = await executionTask.WaitAsync(SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error?.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenOperationWinsBeforeClockExpires_DoesNotReverseToTimeoutDuringContinuation ()
    {
        var timeout = TimeSpan.FromSeconds(1);
        var timeProvider = new ExpiringAfterInitialBudgetReadTimeProvider(timeout);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);

        var result = await ExecutionDeadlineOperation.ExecuteAsync(
            deadline,
            CancellationToken.None,
            "Deadline elapsed before operation.",
            "Deadline elapsed during operation.",
            static _ => ValueTask.FromResult("operation value"));

        Assert.True(result.IsSuccess);
        Assert.Equal("operation value", result.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenCallerCancellationWinsImmediatelyBeforeLateFault_ThrowsCallerCancellationAndReleasesOperationCancellation ()
    {
        var timeProvider = new ManualTimeProvider();
        var operationSource = new InlineCompletingValueTaskSource<string>();
        var operationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callerCancellationTokenSource = new CancellationTokenSource();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);
        var operationCancellationToken = CancellationToken.None;
        var executionTask = ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                callerCancellationTokenSource.Token,
                "Deadline elapsed before operation.",
                "Deadline elapsed during operation.",
                cancellationToken =>
                {
                    operationCancellationToken = cancellationToken;
                    _ = cancellationToken.UnsafeRegister(
                        static state => ((TaskCompletionSource)state!).TrySetResult(),
                        operationCancellationObserved);
                    operationEntered.TrySetResult();
                    return operationSource.CreateValueTask();
                })
            .AsTask();
        await operationEntered.Task.WaitAsync(SignalWaitTimeout);
        await operationSource.ContinuationRegistered.WaitAsync(SignalWaitTimeout);

        callerCancellationTokenSource.Cancel();
        operationSource.SetException(new InvalidOperationException("late fault"));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => executionTask.WaitAsync(SignalWaitTimeout));
        await operationCancellationObserved.Task.WaitAsync(SignalWaitTimeout);
        await WaitForCancellationTokenSourceDisposalAsync(operationCancellationToken);

        Assert.Equal(callerCancellationTokenSource.Token, exception.CancellationToken);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    private static async Task WaitForCancellationTokenSourceDisposalAsync (CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                _ = cancellationToken.WaitHandle;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            await Task.Yield();
        }

        Assert.Fail("Operation cancellation token source was not disposed after late operation completion.");
    }

    private sealed class InlineCompletingValueTaskSource<T> : IValueTaskSource<T>
    {
        private ManualResetValueTaskSourceCore<T> source;

        private readonly TaskCompletionSource continuationRegistered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public InlineCompletingValueTaskSource ()
        {
            source.RunContinuationsAsynchronously = false;
        }

        public Task ContinuationRegistered => continuationRegistered.Task;

        public ValueTask<T> CreateValueTask ()
        {
            return new ValueTask<T>(this, source.Version);
        }

        public T GetResult (short token)
        {
            return source.GetResult(token);
        }

        public ValueTaskSourceStatus GetStatus (short token)
        {
            return source.GetStatus(token);
        }

        public void OnCompleted (
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags)
        {
            source.OnCompleted(continuation, state, token, flags);
            continuationRegistered.TrySetResult();
        }

        public void SetException (Exception exception)
        {
            source.SetException(exception);
        }

        public void SetResult (T result)
        {
            source.SetResult(result);
        }
    }

    private sealed class ExpiringAfterInitialBudgetReadTimeProvider : TimeProvider
    {
        private readonly long expiredTimestamp;

        private int timestampReadCount;

        public ExpiringAfterInitialBudgetReadTimeProvider (TimeSpan timeout)
        {
            expiredTimestamp = timeout.Ticks;
        }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp ()
        {
            return Interlocked.Increment(ref timestampReadCount) <= 2
                ? 0
                : expiredTimestamp;
        }

        public override ITimer CreateTimer (
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            return new InertTimer();
        }

        private sealed class InertTimer : ITimer
        {
            public bool Change (
                TimeSpan dueTime,
                TimeSpan period)
            {
                return true;
            }

            public void Dispose ()
            {
            }

            public ValueTask DisposeAsync ()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
