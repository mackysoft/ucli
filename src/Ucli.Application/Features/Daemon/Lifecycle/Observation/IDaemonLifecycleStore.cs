using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Persists daemon lifecycle observations that can be read when IPC endpoint is unavailable. </summary>
internal interface IDaemonLifecycleStore
{
    /// <summary> Reads the lifecycle observation for one project fingerprint. </summary>
    ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Deletes the lifecycle observation for one project fingerprint. </summary>
    ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default);
}
