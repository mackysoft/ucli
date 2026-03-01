namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Daemon;

public sealed class FileSystemDaemonLifecycleLockProviderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WhenLockAlreadyHeld_WaitsUntilReleased ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "wait-until-release");
        var provider = new FileSystemDaemonLifecycleLockProvider();
        var firstHandle = await provider.Acquire(scope.FullPath, "fingerprint-lock", CancellationToken.None);
        using var acquireCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var secondAcquireTask = provider.Acquire(scope.FullPath, "fingerprint-lock", acquireCts.Token).AsTask();

        await Task.Delay(150, CancellationToken.None);
        Assert.False(secondAcquireTask.IsCompleted);

        await firstHandle.DisposeAsync();
        var secondHandle = await secondAcquireTask;
        await secondHandle.DisposeAsync();
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WhenCanceledWhileWaiting_ThrowsOperationCanceledException ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "cancel-while-waiting");
        var provider = new FileSystemDaemonLifecycleLockProvider();
        var firstHandle = await provider.Acquire(scope.FullPath, "fingerprint-lock", CancellationToken.None);
        using var waitingCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var exception = await Record.ExceptionAsync(async () =>
        {
            await provider.Acquire(scope.FullPath, "fingerprint-lock", waitingCts.Token);
        });

        await firstHandle.DisposeAsync();

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }
}
