using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements filesystem-backed lifecycle locks scoped by storage root and project fingerprint. </summary>
internal sealed class FileSystemDaemonLifecycleLockProvider : IDaemonLifecycleLockProvider
{
    private const int RetryDelayMilliseconds = 50;

    /// <summary> Acquires the lifecycle lock for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The async-disposable lock handle that must be disposed to release lock. </returns>
    /// <exception cref="ArgumentException"> Thrown when one argument is <see langword="null" />, empty, or whitespace. </exception>
    public async ValueTask<IAsyncDisposable> Acquire (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        if (string.IsNullOrWhiteSpace(projectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }

        var lockFilePath = UcliStoragePathResolver.ResolveLifecycleLockPath(storageRoot, projectFingerprint);
        var lockDirectoryPath = Path.GetDirectoryName(lockFilePath);
        if (!string.IsNullOrWhiteSpace(lockDirectoryPath))
        {
            Directory.CreateDirectory(lockDirectoryPath);
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var stream = new FileStream(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
                return new LockHandle(stream);
            }
            catch (IOException)
            {
                await Task.Delay(RetryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary> Releases one file lock handle when disposed. </summary>
    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly FileStream lockStream;

        private bool disposed;

        /// <summary> Initializes a new instance of the <see cref="LockHandle" /> class. </summary>
        /// <param name="lockStream"> The lock file stream to release on dispose. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="lockStream" /> is <see langword="null" />. </exception>
        public LockHandle (FileStream lockStream)
        {
            this.lockStream = lockStream ?? throw new ArgumentNullException(nameof(lockStream));
        }

        /// <summary> Releases the held file lock once. </summary>
        /// <returns> A completed task. </returns>
        public ValueTask DisposeAsync ()
        {
            if (disposed)
            {
                return ValueTask.CompletedTask;
            }

            disposed = true;
            lockStream.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}