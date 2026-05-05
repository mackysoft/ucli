namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary> Provides project-fingerprint scoped asynchronous lifecycle locks for Unity project execution operations. </summary>
internal interface IProjectLifecycleLockProvider
{
    /// <summary> Acquires the lifecycle lock for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="timeout"> The timeout budget used while waiting for lock acquisition. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The async-disposable lock handle that must be disposed to release lock. </returns>
    ValueTask<IAsyncDisposable> Acquire (
        string storageRoot,
        string projectFingerprint,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
