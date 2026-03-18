namespace MackySoft.Ucli.Tests.Execution;

using MackySoft.Tests;
using MackySoft.Ucli.Execution;

public sealed class InMemoryProjectLifecycleLockProviderTests
{
    private static readonly TimeSpan AcquireWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WhenEquivalentStorageRootsAreUsed_UsesSameLockScope ()
    {
        using var scope = TestDirectories.CreateTempScope("project-lifecycle-lock", "in-memory-equivalent-storage-roots");
        var timeProvider = new ManualTimeProvider();
        var provider = new InMemoryProjectLifecycleLockProvider(timeProvider);
        var firstHandle = await provider.Acquire(
            scope.FullPath,
            "fingerprint-lock",
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        var secondAcquireTask = provider.Acquire(
            Path.Combine(scope.FullPath, "."),
            "fingerprint-lock",
            TimeSpan.FromSeconds(2),
            CancellationToken.None).AsTask();

        Assert.False(secondAcquireTask.IsCompleted);

        await firstHandle.DisposeAsync();
        var secondHandle = await TestAwaiter.WaitAsync(secondAcquireTask, "In-memory lifecycle lock reacquire", AcquireWaitTimeout);
        await secondHandle.DisposeAsync();
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WhenWaiterIsGranted_CancelsPendingDelayTimer ()
    {
        using var scope = TestDirectories.CreateTempScope("project-lifecycle-lock", "in-memory-granted-waiter-cancels-delay");
        var timeProvider = new ManualTimeProvider();
        var provider = new InMemoryProjectLifecycleLockProvider(timeProvider);
        var firstHandle = await provider.Acquire(
            scope.FullPath,
            "fingerprint-lock",
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        var secondAcquireTask = provider.Acquire(
                scope.FullPath,
                "fingerprint-lock",
                TimeSpan.FromSeconds(30),
                CancellationToken.None)
            .AsTask();

        Assert.Equal(1, timeProvider.ActiveTimerCount);

        await firstHandle.DisposeAsync();
        var secondHandle = await TestAwaiter.WaitAsync(secondAcquireTask, "In-memory lifecycle lock granted waiter", AcquireWaitTimeout);

        Assert.Equal(0, timeProvider.ActiveTimerCount);

        await secondHandle.DisposeAsync();
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WhenTimeoutWhileWaiting_ThrowsTimeoutException ()
    {
        using var scope = TestDirectories.CreateTempScope("project-lifecycle-lock", "in-memory-timeout-while-waiting");
        var timeProvider = new ManualTimeProvider();
        var provider = new InMemoryProjectLifecycleLockProvider(timeProvider);
        var firstHandle = await provider.Acquire(
            scope.FullPath,
            "fingerprint-lock",
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        var waitingTask = provider.Acquire(
                scope.FullPath,
                "fingerprint-lock",
                TimeSpan.FromMilliseconds(150),
                CancellationToken.None)
            .AsTask();
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));
        var exception = await Record.ExceptionAsync(async () =>
        {
            await TestAwaiter.WaitAsync(waitingTask, "Timed out in-memory lifecycle lock acquire", AcquireWaitTimeout);
        });

        await firstHandle.DisposeAsync();

        Assert.IsType<TimeoutException>(exception);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WhenLockIsReleasedJustBeforeTimeout_AcquiresLock ()
    {
        using var scope = TestDirectories.CreateTempScope("project-lifecycle-lock", "in-memory-timeout-boundary-release");
        var timeProvider = new ManualTimeProvider();
        var provider = new InMemoryProjectLifecycleLockProvider(timeProvider);
        var firstHandle = await provider.Acquire(
            scope.FullPath,
            "fingerprint-lock",
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        using var releaseTimer = timeProvider.CreateTimer(
            static state =>
            {
                ((IAsyncDisposable)state!).DisposeAsync().GetAwaiter().GetResult();
            },
            firstHandle,
            TimeSpan.FromMilliseconds(149),
            System.Threading.Timeout.InfiniteTimeSpan);

        var secondAcquireTask = provider.Acquire(
                scope.FullPath,
                "fingerprint-lock",
                TimeSpan.FromMilliseconds(150),
                CancellationToken.None)
            .AsTask();

        Assert.False(secondAcquireTask.IsCompleted);

        timeProvider.Advance(TimeSpan.FromMilliseconds(149));
        var secondHandle = await TestAwaiter.WaitAsync(secondAcquireTask, "Boundary in-memory lifecycle lock acquire", AcquireWaitTimeout);
        await secondHandle.DisposeAsync();
    }
}