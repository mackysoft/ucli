using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.TestSupport;

internal sealed class NonCooperativeBlockingDaemonSessionStore : ReadOnlyDaemonSessionStore
{
    private readonly int blockOnCall;

    private readonly Queue<DaemonSessionReadResult> results;

    private readonly TaskCompletionSource<bool> blockedSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<bool> releaseSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int callCount;

    public NonCooperativeBlockingDaemonSessionStore (
        int blockOnCall,
        params DaemonSessionReadResult[] results)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(blockOnCall, 1);
        ArgumentNullException.ThrowIfNull(results);
        if (results.Length < blockOnCall)
        {
            throw new ArgumentException("A result is required for every call through the blocking call.", nameof(results));
        }

        this.blockOnCall = blockOnCall;
        this.results = new Queue<DaemonSessionReadResult>(results);
    }

    public Task Blocked => blockedSource.Task;

    public override ValueTask<DaemonSessionReadResult> ReadAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = results.Dequeue();
        var currentCall = Interlocked.Increment(ref callCount);
        if (currentCall != blockOnCall)
        {
            return ValueTask.FromResult(result);
        }

        return new ValueTask<DaemonSessionReadResult>(WaitForReleaseAsync(result));
    }

    public void Release ()
    {
        releaseSource.TrySetResult(true);
    }

    private async Task<DaemonSessionReadResult> WaitForReleaseAsync (
        DaemonSessionReadResult result)
    {
        blockedSource.TrySetResult(true);
        await releaseSource.Task;
        return result;
    }
}
