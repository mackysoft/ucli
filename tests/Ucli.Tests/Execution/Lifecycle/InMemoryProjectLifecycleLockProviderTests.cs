namespace MackySoft.Ucli.Tests.Execution;

using MackySoft.Tests;
using MackySoft.Ucli.Execution;

public sealed class InMemoryProjectLifecycleLockProviderTests
{
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
        var secondHandle = await secondAcquireTask;
        await secondHandle.DisposeAsync();
    }
}