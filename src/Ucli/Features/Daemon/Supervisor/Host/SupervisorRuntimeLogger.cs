using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Shared.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Writes supervisor runtime logs under the worktree-local storage root. </summary>
internal sealed class SupervisorRuntimeLogger
{
    private readonly SemaphoreSlim writeGate = new(1, 1);

    /// <summary> Appends one structured runtime-log line. </summary>
    /// <param name="storageRoot"> The worktree-local storage root. </param>
    /// <param name="level"> The log level label. </param>
    /// <param name="message"> The log message body. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    public async Task Write (
        string storageRoot,
        string level,
        string message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var logPath = UcliStoragePathResolver.ResolveSupervisorLogPath(storageRoot);
        var logDirectoryPath = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrWhiteSpace(logDirectoryPath))
        {
            FileSystemAccessBoundary.EnsureSecureDirectory(logDirectoryPath);
        }

        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var line = $"{DateTimeOffset.UtcNow:O} [{level}] {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(logPath, line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeGate.Release();
        }
    }
}