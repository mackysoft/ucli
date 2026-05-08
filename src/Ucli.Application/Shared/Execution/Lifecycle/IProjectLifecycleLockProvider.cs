namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary> Provides physical Unity-project scoped asynchronous lifecycle locks for Unity project execution operations. </summary>
internal interface IProjectLifecycleLockProvider
{
    /// <summary> Acquires the lifecycle lock for one physical Unity project root. </summary>
    /// <param name="request"> The lifecycle lock request whose Unity project root scopes the lock. </param>
    /// <param name="timeout"> The timeout budget used while waiting for lock acquisition. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The async-disposable lock handle that must be disposed to release lock. </returns>
    ValueTask<IAsyncDisposable> Acquire (
        ProjectLifecycleLockRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
