using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Applies same-user filesystem boundary rules for one unix-domain-socket listener path. </summary>
internal sealed class UnixSocketAccessBoundary
{
    private readonly AbsolutePath socketDirectoryPath;

    private readonly AbsolutePath socketPath;

    /// <summary> Initializes a new instance of the <see cref="UnixSocketAccessBoundary" /> class. </summary>
    /// <param name="authorizedSocketPath"> The exact unix-domain-socket path authorized before filesystem access. </param>
    public UnixSocketAccessBoundary (AbsolutePath authorizedSocketPath)
    {
        if (authorizedSocketPath is null)
        {
            throw new ArgumentNullException(nameof(authorizedSocketPath));
        }

        if (!authorizedSocketPath.TryGetParent(out var parentDirectoryPath))
        {
            throw new ArgumentException(
                "Authorized socket path must have a parent directory.",
                nameof(authorizedSocketPath));
        }

        socketDirectoryPath = parentDirectoryPath;
        socketPath = authorizedSocketPath;
    }

    /// <summary> Ensures the socket directory is secure and removes stale socket residue before bind. </summary>
    public void PrepareForBind ()
    {
        FileSystemAccessBoundary.EnsureSecureDirectory(socketDirectoryPath);
        FileUtilities.DeleteIfExists(socketPath);
    }

    /// <summary> Applies owner-only mode to the bound socket node. </summary>
    public void HardenBoundSocket ()
    {
        FileSystemAccessBoundary.EnsureSecureUnixSocket(socketPath);
    }

    /// <summary> Removes the exact socket node without deleting its parent directory. </summary>
    public void Cleanup ()
    {
        FileUtilities.DeleteIfExists(socketPath);
    }
}
