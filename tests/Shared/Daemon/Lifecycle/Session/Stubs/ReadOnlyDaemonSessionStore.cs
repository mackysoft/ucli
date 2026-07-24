using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.TestSupport;

internal abstract class ReadOnlyDaemonSessionStore : IDaemonSessionStore
{
    public abstract ValueTask<DaemonSessionReadResult> ReadAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default);

    public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
        AbsolutePath storageRoot,
        DaemonSession session,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This daemon session store supports reads only.");
    }

    public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This daemon session store supports reads only.");
    }
}
