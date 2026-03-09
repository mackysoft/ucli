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
        var provider = new InMemoryProjectLifecycleLockProvider();
        var firstHandle = await provider.Acquire(
            scope.FullPath,
            "fingerprint-lock",
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        using var acquireCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var secondAcquireTask = provider.Acquire(
            Path.Combine(scope.FullPath, "."),
            "fingerprint-lock",
            TimeSpan.FromSeconds(2),
            acquireCts.Token).AsTask();

        await Task.Delay(150, CancellationToken.None);
        Assert.False(secondAcquireTask.IsCompleted);

        await firstHandle.DisposeAsync();
        var secondHandle = await secondAcquireTask;
        await secondHandle.DisposeAsync();
    }
}