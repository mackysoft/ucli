using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

namespace MackySoft.Ucli.TestSupport;

internal sealed class UnexpectedDaemonDiagnosisStore : IDaemonDiagnosisStore
{
    private readonly string reason;

    public UnexpectedDaemonDiagnosisStore (string reason)
    {
        this.reason = reason;
    }

    public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        DaemonDiagnosis diagnosis,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }
}
