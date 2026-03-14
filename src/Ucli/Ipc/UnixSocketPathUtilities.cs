using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Ipc;

/// <summary> Provides deterministic unix-domain-socket fallback path utilities. </summary>
internal static class UnixSocketPathUtilities
{
    /// <summary> Builds one deterministic fallback unix-domain-socket path under the current temp root. </summary>
    /// <param name="directoryPrefix"> The dedicated fallback directory prefix. </param>
    /// <param name="identitySource"> The stable identity source used for hashing. </param>
    /// <returns> The deterministic fallback socket path. </returns>
    public static string BuildFallbackSocketPath (
        string directoryPrefix,
        string identitySource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(identitySource);

        var normalizedTempRoot = NormalizeDirectoryPath(Path.GetTempPath());
        var hashHex = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(identitySource));
        var basePath = Path.Combine(normalizedTempRoot, directoryPrefix, UcliIpcEndpointNames.UnixSocketFileName);
        var availableHashLength = IpcTransportConstraints.UnixDomainSocketPathMaxBytes - Encoding.UTF8.GetByteCount(basePath);
        if (availableHashLength <= 0)
        {
            throw new InvalidOperationException(
                $"Unix socket fallback path exceeds platform limit before hash is added. TempRoot={normalizedTempRoot}, Prefix={directoryPrefix}.");
        }

        var hashLength = Math.Min(hashHex.Length, availableHashLength);
        var directoryName = directoryPrefix + hashHex[..hashLength];
        return Path.Combine(normalizedTempRoot, directoryName, UcliIpcEndpointNames.UnixSocketFileName);
    }

    /// <summary> Deletes one empty fallback directory when the socket path uses the dedicated temp-root pattern. </summary>
    /// <param name="socketPath"> The socket path to inspect. </param>
    /// <param name="directoryPrefix"> The dedicated fallback directory prefix. </param>
    public static void DeleteEmptyFallbackDirectoryIfPresent (
        string socketPath,
        string directoryPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPrefix);

        if (!TryResolveFallbackDirectoryPath(socketPath, directoryPrefix, out var directoryPath))
        {
            return;
        }

        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(directoryPath).Any())
        {
            return;
        }

        Directory.Delete(directoryPath);
    }

    private static bool TryResolveFallbackDirectoryPath (
        string socketPath,
        string directoryPrefix,
        out string directoryPath)
    {
        directoryPath = string.Empty;

        if (!string.Equals(Path.GetFileName(socketPath), UcliIpcEndpointNames.UnixSocketFileName, StringComparison.Ordinal))
        {
            return false;
        }

        var normalizedSocketPath = Path.GetFullPath(socketPath);
        var parentDirectoryPath = Path.GetDirectoryName(normalizedSocketPath);
        if (string.IsNullOrWhiteSpace(parentDirectoryPath))
        {
            return false;
        }

        var normalizedParentDirectoryPath = NormalizeDirectoryPath(parentDirectoryPath);
        var normalizedTempRoot = NormalizeDirectoryPath(Path.GetTempPath());
        var parentOfDirectoryPath = Path.GetDirectoryName(normalizedParentDirectoryPath.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(parentOfDirectoryPath))
        {
            return false;
        }

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(
                NormalizeDirectoryPath(parentOfDirectoryPath),
                normalizedTempRoot,
                pathComparison))
        {
            return false;
        }

        var directoryName = Path.GetFileName(normalizedParentDirectoryPath.TrimEnd(Path.DirectorySeparatorChar));
        if (!directoryName.StartsWith(directoryPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        directoryPath = normalizedParentDirectoryPath;
        return true;
    }

    private static string NormalizeDirectoryPath (string directoryPath)
    {
        var normalizedDirectoryPath = Path.GetFullPath(directoryPath);
        return normalizedDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}