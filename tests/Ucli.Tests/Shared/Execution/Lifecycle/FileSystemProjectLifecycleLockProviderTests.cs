namespace MackySoft.Ucli.Tests.Execution;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;

public sealed class FileSystemProjectLifecycleLockProviderTests
{
    private static readonly TimeSpan AcquireWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WhenLockAlreadyHeld_WaitsUntilReleased ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "wait-until-release");
        var timeProvider = new ManualTimeProvider();
        var provider = new FileSystemProjectLifecycleLockProvider(timeProvider);
        var firstHandle = await provider.Acquire(
            scope.FullPath,
            "fingerprint-lock",
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        var secondAcquireTask = provider.Acquire(
            scope.FullPath,
            "fingerprint-lock",
            TimeSpan.FromSeconds(2),
            CancellationToken.None).AsTask();

        Assert.False(secondAcquireTask.IsCompleted);

        await firstHandle.DisposeAsync();
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        var secondHandle = await TestAwaiter.WaitAsync(secondAcquireTask, "File system lifecycle lock reacquire", AcquireWaitTimeout);
        await secondHandle.DisposeAsync();
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WhenCanceledWhileWaiting_ThrowsOperationCanceledException ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "cancel-while-waiting");
        var timeProvider = new ManualTimeProvider();
        var provider = new FileSystemProjectLifecycleLockProvider(timeProvider);
        var firstHandle = await provider.Acquire(
            scope.FullPath,
            "fingerprint-lock",
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        using var waitingCts = new CancellationTokenSource();

        var waitingTask = provider.Acquire(
                scope.FullPath,
                "fingerprint-lock",
                TimeSpan.FromSeconds(5),
                waitingCts.Token)
            .AsTask();
        Assert.False(waitingTask.IsCompleted);
        waitingCts.Cancel();
        var exception = await Record.ExceptionAsync(async () =>
        {
            await TestAwaiter.WaitAsync(waitingTask, "Canceled file system lifecycle lock acquire", AcquireWaitTimeout);
        });

        await firstHandle.DisposeAsync();

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WhenTimeoutWhileWaiting_ThrowsTimeoutException ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "timeout-while-waiting");
        var timeProvider = new ManualTimeProvider();
        var provider = new FileSystemProjectLifecycleLockProvider(timeProvider);
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
            await TestAwaiter.WaitAsync(waitingTask, "Timed out file system lifecycle lock acquire", AcquireWaitTimeout);
        });

        await firstHandle.DisposeAsync();

        Assert.IsType<TimeoutException>(exception);
    }
}
