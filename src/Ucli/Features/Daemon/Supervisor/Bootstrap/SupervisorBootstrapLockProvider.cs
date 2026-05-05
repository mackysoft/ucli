using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Shared.Execution.Process;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;

/// <summary> Acquires one filesystem-backed bootstrap lock for supervisor start coordination. </summary>
internal sealed class SupervisorBootstrapLockProvider
{
    private const int RetryDelayMilliseconds = 50;

    /// <summary> Acquires the bootstrap lock for the specified storage root. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="timeout"> The timeout budget. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The async-disposable lock handle. </returns>
    public async ValueTask<IAsyncDisposable> Acquire (
        string storageRoot,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var lockFilePath = UcliStoragePathResolver.ResolveSupervisorBootstrapLockPath(storageRoot);
        var lockDirectoryPath = Path.GetDirectoryName(lockFilePath);
        if (!string.IsNullOrWhiteSpace(lockDirectoryPath))
        {
            FileSystemAccessBoundary.EnsureSecureDirectory(lockDirectoryPath);
        }

        var deadline = ExecutionDeadline.Start(timeout);
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

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
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
