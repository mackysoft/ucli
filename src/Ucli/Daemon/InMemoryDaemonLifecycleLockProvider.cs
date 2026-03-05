using System.Collections.Concurrent;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements process-local in-memory lifecycle locks scoped by storage root and project fingerprint. </summary>
internal sealed class InMemoryDaemonLifecycleLockProvider : IDaemonLifecycleLockProvider
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> LocksByFingerprint = new(StringComparer.Ordinal);

    /// <summary> Acquires the lifecycle lock for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="timeout"> The timeout budget used while waiting for lock acquisition. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The async-disposable lock handle that must be disposed to release lock. </returns>
    /// <exception cref="ArgumentException"> Thrown when one argument is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="TimeoutException"> Thrown when lock acquisition exceeds <paramref name="timeout" />. </exception>
    public async ValueTask<IAsyncDisposable> Acquire (
        string storageRoot,
        string projectFingerprint,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(projectFingerprint, out var normalizedProjectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var lockKey = $"{Path.GetFullPath(storageRoot)}\n{normalizedProjectFingerprint}";
        var semaphore = LocksByFingerprint.GetOrAdd(
            lockKey,
            static _ => new SemaphoreSlim(1, 1));
        var acquired = await semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (!acquired)
        {
            throw new TimeoutException(
                $"Timed out while waiting to acquire daemon lifecycle lock. Timeout={timeout.TotalMilliseconds:0}ms.");
        }

        return new LockHandle(semaphore);
    }

    /// <summary> Releases one semaphore lock when disposed. </summary>
    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly SemaphoreSlim semaphore;

        private bool disposed;

        /// <summary> Initializes a new instance of the <see cref="LockHandle" /> class. </summary>
        /// <param name="semaphore"> The semaphore to release on dispose. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="semaphore" /> is <see langword="null" />. </exception>
        public LockHandle (SemaphoreSlim semaphore)
        {
            this.semaphore = semaphore ?? throw new ArgumentNullException(nameof(semaphore));
        }

        /// <summary> Releases the held semaphore lock once. </summary>
        /// <returns> A completed task. </returns>
        public ValueTask DisposeAsync ()
        {
            if (disposed)
            {
                return ValueTask.CompletedTask;
            }

            disposed = true;
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}