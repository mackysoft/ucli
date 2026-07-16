namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Provides persistence access for daemon session metadata. </summary>
internal interface IDaemonSessionStore
{
    /// <summary> Reads daemon session metadata for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session read result. </returns>
    ValueTask<DaemonSessionReadResult> ReadAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Writes daemon session metadata to local storage. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="session"> The daemon session metadata to persist. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session storage operation result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
        string storageRoot,
        DaemonSession session,
        CancellationToken cancellationToken = default);

    /// <summary> Deletes daemon session metadata for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session storage operation result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default);
}
