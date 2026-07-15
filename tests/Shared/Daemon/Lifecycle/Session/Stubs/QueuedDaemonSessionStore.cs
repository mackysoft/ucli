using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.TestSupport;

internal sealed class QueuedDaemonSessionStore : IDaemonSessionStore
{
    private readonly Queue<DaemonSessionReadResult> results;

    private DaemonSessionReadResult lastResult;

    public QueuedDaemonSessionStore (params DaemonSessionReadResult[] results)
    {
        ArgumentNullException.ThrowIfNull(results);
        this.results = new Queue<DaemonSessionReadResult>(results);
        lastResult = results.Length == 0 ? DaemonSessionReadResult.Missing() : results[^1];
    }

    public ValueTask<DaemonSessionReadResult> ReadAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (results.Count != 0)
        {
            lastResult = results.Dequeue();
        }

        return ValueTask.FromResult(lastResult);
    }

    public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
        string storageRoot,
        DaemonSession session,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Queued daemon session store supports reads only.");
    }

    public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Queued daemon session store supports reads only.");
    }
}
