using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.TestSupport;

internal abstract class ReadOnlyDaemonSessionStore : IDaemonSessionStore
{
    public abstract ValueTask<DaemonSessionReadResult> ReadAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default);

    public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
        string storageRoot,
        DaemonSession session,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This daemon session store supports reads only.");
    }

    public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This daemon session store supports reads only.");
    }
}
