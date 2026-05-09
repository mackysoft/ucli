namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

/// <summary> Provides persistence access for daemon diagnosis metadata. </summary>
internal interface IDaemonDiagnosisStore
{
    /// <summary> Reads daemon diagnosis metadata for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon diagnosis read result. </returns>
    ValueTask<DaemonDiagnosisReadResult> ReadAsync (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Writes daemon diagnosis metadata to local storage. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="diagnosis"> The daemon diagnosis metadata to persist. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon diagnosis storage operation result. </returns>
    ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
        string storageRoot,
        string projectFingerprint,
        DaemonDiagnosis diagnosis,
        CancellationToken cancellationToken = default);

    /// <summary> Deletes daemon diagnosis metadata for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon diagnosis storage operation result. </returns>
    ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);
}
