using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;

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
    public async Task WriteAsync (
        AbsolutePath storageRoot,
        string level,
        string message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var logPath = UcliStoragePathResolver.ResolveSupervisorLogPath(storageRoot);
        if (!logPath.TryGetParent(out var logDirectoryPath))
        {
            throw new InvalidOperationException(
                $"Supervisor log directory could not be resolved: {logPath.Value}");
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(logDirectoryPath);
        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var line = $"{DateTimeOffset.UtcNow:O} [{level}] {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(logPath.Value, line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeGate.Release();
        }
    }
}
