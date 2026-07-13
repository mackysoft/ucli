namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Persists daemon lifecycle observations that can be read when IPC endpoint is unavailable. </summary>
internal interface IDaemonLifecycleStore
{
    /// <summary> Reads the lifecycle observation for one project fingerprint. </summary>
    ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Deletes the lifecycle observation for one project fingerprint. </summary>
    ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default);
}
