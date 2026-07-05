using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.TestSupport;

internal sealed class UnexpectedDaemonSessionStore : IDaemonSessionStore
{
    private readonly string reason;

    public UnexpectedDaemonSessionStore (string reason = "Daemon session store access was not expected.")
    {
        this.reason = reason;
    }

    public ValueTask<DaemonSessionReadResult> ReadAsync (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
        string storageRoot,
        DaemonSession session,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }
}
