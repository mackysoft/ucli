using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;

/// <summary> Acquires one filesystem-backed bootstrap lock for supervisor start coordination. </summary>
internal sealed class SupervisorBootstrapLockProvider
{
    private const int RetryDelayMilliseconds = 50;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="SupervisorBootstrapLockProvider" /> class. </summary>
    /// <param name="timeProvider"> The time provider used for lock acquisition deadlines and retry delays. </param>
    public SupervisorBootstrapLockProvider (TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Acquires the bootstrap lock for the specified storage root. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="timeout"> The timeout budget. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The async-disposable lock handle. </returns>
    public async ValueTask<IAsyncDisposable> AcquireAsync (
        AbsolutePath storageRoot,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var lockFilePath = UcliStoragePathResolver.ResolveSupervisorBootstrapLockPath(storageRoot);
        if (!lockFilePath.TryGetParent(out var lockDirectoryPath))
        {
            throw new InvalidOperationException(
                $"Supervisor bootstrap lock directory could not be resolved: {lockFilePath.Value}");
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(lockDirectoryPath);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var stream = new FileStream(
                    lockFilePath.Value,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
                return new LockHandle(stream);
            }
            catch (IOException)
            {
                if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
                {
                    throw new TimeoutException(
                        $"Timed out while waiting to acquire supervisor bootstrap lock. Timeout={timeout.TotalMilliseconds:0}ms.");
                }

                var retryDelay = TimeSpan.FromMilliseconds(RetryDelayMilliseconds);
                var delay = remainingTimeout < retryDelay
                    ? remainingTimeout
                    : retryDelay;
                if (delay <= TimeSpan.Zero)
                {
                    delay = TimeSpan.FromMilliseconds(1);
                }

                await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly FileStream lockStream;

        private bool disposed;

        public LockHandle (FileStream lockStream)
        {
            this.lockStream = lockStream ?? throw new ArgumentNullException(nameof(lockStream));
        }

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
