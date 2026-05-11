using System.Runtime.InteropServices;
using System.Text;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Resolves shared uCLI IPC endpoints from storage and project identity values. </summary>
internal static class UcliIpcEndpointResolver
{
    /// <summary> Resolves the daemon transport endpoint for the given project identity. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The resolved daemon transport endpoint. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> or <paramref name="projectFingerprint" /> is empty. </exception>
    public static IpcEndpoint ResolveDaemonEndpoint (
        string storageRoot,
        string projectFingerprint)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(projectFingerprint, out var normalizedProjectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }

        var normalizedStorageRoot = Path.GetFullPath(storageRoot);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pipeName = UcliIpcEndpointNames.DaemonAddressPrefix + normalizedProjectFingerprint;
            return new IpcEndpoint(IpcTransportKind.NamedPipe, pipeName);
        }

        var preferredSocketPath = Path.Combine(
            UcliStoragePathResolver.ResolveFingerprintDirectory(normalizedStorageRoot, normalizedProjectFingerprint),
            UcliIpcEndpointNames.UnixSocketFileName);

        if (Encoding.UTF8.GetByteCount(preferredSocketPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, preferredSocketPath);
        }

        var fallbackSocketPath = BuildFallbackUnixSocketPath(
            normalizedStorageRoot,
            normalizedProjectFingerprint);
        return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, fallbackSocketPath);
    }

    /// <summary> Resolves the GUI supervisor transport endpoint for the given project identity. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The resolved GUI supervisor transport endpoint. </returns>
    public static IpcEndpoint ResolveGuiSupervisorEndpoint (
        string storageRoot,
        string projectFingerprint)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(projectFingerprint, out var normalizedProjectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }

        var normalizedStorageRoot = Path.GetFullPath(storageRoot);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pipeName = UcliIpcEndpointNames.GuiSupervisorAddressPrefix + normalizedProjectFingerprint;
            return new IpcEndpoint(IpcTransportKind.NamedPipe, pipeName);
        }

        var preferredSocketPath = Path.Combine(
            UcliStoragePathResolver.ResolveFingerprintDirectory(normalizedStorageRoot, normalizedProjectFingerprint),
            "gui-supervisor.sock");

        if (Encoding.UTF8.GetByteCount(preferredSocketPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, preferredSocketPath);
        }

        var fallbackSocketPath = UnixSocketPathUtilities.BuildFallbackSocketPath(
            UcliIpcEndpointNames.GuiSupervisorAddressPrefix,
            $"{normalizedStorageRoot}\n{normalizedProjectFingerprint}");
        return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, fallbackSocketPath);
    }

    private static string BuildFallbackUnixSocketPath (
        string normalizedStorageRoot,
        string normalizedProjectFingerprint)
    {
        var hashSource = $"{normalizedStorageRoot}\n{normalizedProjectFingerprint}";
        return UnixSocketPathUtilities.BuildFallbackSocketPath(
            UcliIpcEndpointNames.DaemonAddressPrefix,
            hashSource);
    }
}
