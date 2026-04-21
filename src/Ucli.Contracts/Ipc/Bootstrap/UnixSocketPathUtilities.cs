using System.Runtime.InteropServices;
using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Provides deterministic unix-domain-socket fallback path utilities. </summary>
public static class UnixSocketPathUtilities
{
    /// <summary> Validates one unix-domain-socket path against the shared byte-length limit. </summary>
    /// <param name="socketPath"> The unix-domain-socket path to validate. </param>
    /// <param name="paramName"> The parameter name used when validation fails. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="socketPath" /> is empty or exceeds the shared byte-length limit. </exception>
    public static void ValidateSocketPathLength (
        string socketPath,
        string paramName = "socketPath")
    {
        if (string.IsNullOrWhiteSpace(socketPath))
        {
            throw new ArgumentException("Socket path must not be empty.", paramName);
        }

        var socketPathByteCount = Encoding.UTF8.GetByteCount(socketPath);
        if (socketPathByteCount > IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            throw new ArgumentException(
                "Unix domain socket path exceeds the shared runtime-safe byte-length limit. " +
                $"PathBytes={socketPathByteCount}, MaxBytes={IpcTransportConstraints.UnixDomainSocketPathMaxBytes}, Path={socketPath}",
                paramName);
        }
    }

    /// <summary> Builds one deterministic fallback unix-domain-socket path under the current temp root. </summary>
    /// <param name="directoryPrefix"> The dedicated fallback directory prefix. </param>
    /// <param name="identitySource"> The stable identity source used for hashing. </param>
    /// <returns> The deterministic fallback socket path. </returns>
    public static string BuildFallbackSocketPath (
        string directoryPrefix,
        string identitySource)
    {
        if (string.IsNullOrWhiteSpace(directoryPrefix))
        {
            throw new ArgumentException("Directory prefix must not be empty.", nameof(directoryPrefix));
        }

        if (string.IsNullOrWhiteSpace(identitySource))
        {
            throw new ArgumentException("Identity source must not be empty.", nameof(identitySource));
        }

        var normalizedTempRoot = NormalizeDirectoryPath(Path.GetTempPath());
        // NOTE:
        // Unity's embedded Mono rejects some boundary-length socket paths that are otherwise documented as valid.
        // Keep one shared conservative limit so both the CLI runtime and Unity runtime can bind the same endpoint.
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
        var socketPath = Path.Combine(normalizedTempRoot, directoryName, UcliIpcEndpointNames.UnixSocketFileName);
        ValidateSocketPathLength(socketPath);
        return socketPath;
    }

    /// <summary> Deletes one empty fallback directory when the socket path uses the dedicated temp-root pattern. </summary>
    /// <param name="socketPath"> The socket path to inspect. </param>
    /// <param name="directoryPrefix"> The dedicated fallback directory prefix. </param>
    public static void DeleteEmptyFallbackDirectoryIfPresent (
        string socketPath,
        string directoryPrefix)
    {
        if (string.IsNullOrWhiteSpace(socketPath))
        {
            throw new ArgumentException("Socket path must not be empty.", nameof(socketPath));
        }

        if (string.IsNullOrWhiteSpace(directoryPrefix))
        {
            throw new ArgumentException("Directory prefix must not be empty.", nameof(directoryPrefix));
        }

        if (!TryResolveFallbackDirectoryPath(socketPath, directoryPrefix, out var directoryPath))
        {
            return;
        }

        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        using var enumerator = Directory.EnumerateFileSystemEntries(directoryPath).GetEnumerator();
        if (enumerator.MoveNext())
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

        if (!string.Equals(
                NormalizeDirectoryPath(parentOfDirectoryPath),
                normalizedTempRoot,
                GetPathComparison()))
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

    private static StringComparison GetPathComparison ()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}