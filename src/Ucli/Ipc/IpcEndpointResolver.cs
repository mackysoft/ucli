using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Ipc;

/// <summary> Resolves daemon IPC endpoints from project identity values. </summary>
internal sealed class IpcEndpointResolver : IIpcEndpointResolver
{
    private const string SocketFileName = "ipc.sock";

    private const string PipeNamePrefix = "ucli-";

    private const string UnixSocketFallbackDirectoryPath = "/tmp";

    private const string UnixSocketFallbackFilePrefix = "ucli-";

    private const string UnixSocketFallbackFileExtension = ".sock";

    /// <summary> Resolves the transport endpoint for the given project identity. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The resolved transport endpoint. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />, empty, or whitespace. </exception>
    public IpcEndpoint Resolve (
        string storageRoot,
        string projectFingerprint)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        if (string.IsNullOrWhiteSpace(projectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }

        var normalizedStorageRoot = Path.GetFullPath(storageRoot);
        var normalizedProjectFingerprint = StringValueNormalizer.TrimToNull(projectFingerprint)!;

        if (OperatingSystem.IsWindows())
        {
            var pipeName = PipeNamePrefix + normalizedProjectFingerprint;
            return new IpcEndpoint(IpcTransportKind.NamedPipe, pipeName);
        }

        var preferredSocketPath = Path.Combine(
            UcliStoragePathResolver.ResolveFingerprintDirectory(normalizedStorageRoot, normalizedProjectFingerprint),
            SocketFileName);

        if (Encoding.UTF8.GetByteCount(preferredSocketPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, preferredSocketPath);
        }

        // NOTE:
        // Unix domain socket endpoint path is platform-limited (103 bytes usable on macOS).
        // Prefer the project-local path, but fall back to a deterministic short path when needed.
        var fallbackSocketPath = BuildFallbackUnixSocketPath(
            normalizedStorageRoot,
            normalizedProjectFingerprint);
        return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, fallbackSocketPath);
    }

    private static string BuildFallbackUnixSocketPath (
        string normalizedStorageRoot,
        string normalizedProjectFingerprint)
    {
        var hashSource = $"{normalizedStorageRoot}\n{normalizedProjectFingerprint}";
        var hashHex = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(hashSource));
        var shortHash = hashHex[..32];
        return Path.Combine(
            UnixSocketFallbackDirectoryPath,
            UnixSocketFallbackFilePrefix + shortHash + UnixSocketFallbackFileExtension);
    }
}