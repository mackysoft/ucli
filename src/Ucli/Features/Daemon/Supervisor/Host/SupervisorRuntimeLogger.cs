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
