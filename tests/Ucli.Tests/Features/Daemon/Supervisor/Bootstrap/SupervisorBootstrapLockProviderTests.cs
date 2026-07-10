using MackySoft.Tests;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorBootstrapLockProviderTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WhenLockRemainsHeld_ExpiresWithInjectedTimeProvider ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrap-lock", "manual-timeout");
        var timeProvider = new ManualTimeProvider();
        var provider = new SupervisorBootstrapLockProvider(timeProvider);
        await using var heldLock = await provider.AcquireAsync(
            scope.FullPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        var acquisitionTask = provider.AcquireAsync(
                scope.FullPath,
                TimeSpan.FromMilliseconds(100),
                CancellationToken.None)
            .AsTask();
        await timeProvider
            .WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(50))
            .WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));

        _ = await Assert.ThrowsAsync<TimeoutException>(
            () => acquisitionTask.WaitAsync(TimeSpan.FromSeconds(1)));
    }
}
