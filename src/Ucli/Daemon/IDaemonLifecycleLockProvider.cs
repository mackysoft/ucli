namespace MackySoft.Ucli.Daemon;

/// <summary> Provides project-fingerprint scoped asynchronous lifecycle locks for daemon management operations. </summary>
internal interface IDaemonLifecycleLockProvider
{
    /// <summary> Acquires the lifecycle lock for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The async-disposable lock handle that must be disposed to release lock. </returns>
    ValueTask<IAsyncDisposable> Acquire (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);
}