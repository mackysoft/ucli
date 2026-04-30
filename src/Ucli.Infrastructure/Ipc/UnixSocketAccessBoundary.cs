using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Applies same-user filesystem boundary rules for one unix-domain-socket listener path. </summary>
internal sealed class UnixSocketAccessBoundary
{
    private readonly string socketPath;

    private readonly string fallbackDirectoryPrefix;

    /// <summary> Initializes a new instance of the <see cref="UnixSocketAccessBoundary" /> class. </summary>
    /// <param name="socketPath"> The target unix-domain-socket path. </param>
    /// <param name="fallbackDirectoryPrefix"> The fallback directory prefix used for cleanup. </param>
    public UnixSocketAccessBoundary (
        string socketPath,
        string fallbackDirectoryPrefix)
    {
        if (string.IsNullOrWhiteSpace(socketPath))
        {
            throw new ArgumentException("Socket path must not be empty.", nameof(socketPath));
        }

        if (string.IsNullOrWhiteSpace(fallbackDirectoryPrefix))
        {
            throw new ArgumentException("Fallback directory prefix must not be empty.", nameof(fallbackDirectoryPrefix));
        }

        this.socketPath = socketPath;
        this.fallbackDirectoryPrefix = fallbackDirectoryPrefix;
    }

    /// <summary> Ensures the socket directory is secure and removes stale socket residue before bind. </summary>
    public void PrepareForBind ()
    {
        var socketDirectoryPath = Path.GetDirectoryName(socketPath);
        if (!string.IsNullOrWhiteSpace(socketDirectoryPath))
        {
            FileSystemAccessBoundary.EnsureSecureDirectory(socketDirectoryPath);
        }

        FileUtilities.DeleteIfExists(socketPath);
    }

    /// <summary> Applies owner-only mode to the bound socket node. </summary>
    public void HardenBoundSocket ()
    {
        FileSystemAccessBoundary.EnsureSecureUnixSocket(socketPath);
    }

    /// <summary> Removes stale socket residue and cleans up empty fallback directory when applicable. </summary>
    public void Cleanup ()
    {
        FileUtilities.DeleteIfExists(socketPath);
        UnixSocketPathUtilities.DeleteEmptyFallbackDirectoryIfPresent(
            socketPath,
            fallbackDirectoryPrefix);
    }
}
