using System.Runtime.InteropServices;
using System.Text;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Resolves shared uCLI IPC endpoints from storage and project identity values. </summary>
internal static class UcliIpcEndpointResolver
{
    /// <summary> Resolves the daemon transport endpoint for the given project identity. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The resolved daemon transport endpoint. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectFingerprint" /> is <see langword="null" />. </exception>
    public static IpcEndpoint ResolveDaemonEndpoint (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        if (projectFingerprint == null)
        {
            throw new ArgumentNullException(nameof(projectFingerprint));
        }

        var normalizedStorageRoot = UcliStoragePathResolver.NormalizeStorageRootPath(storageRoot);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pipeName = UcliIpcEndpointNames.DaemonAddressPrefix + projectFingerprint;
            return new IpcEndpoint(IpcTransportKind.NamedPipe, pipeName);
        }

        var preferredSocketPath = Path.Combine(
            UcliStoragePathResolver.ResolveFingerprintDirectory(normalizedStorageRoot, projectFingerprint),
            UcliIpcEndpointNames.UnixSocketFileName);

        if (Encoding.UTF8.GetByteCount(preferredSocketPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, preferredSocketPath);
        }

        var fallbackSocketPath = BuildFallbackUnixSocketPath(
            normalizedStorageRoot,
            projectFingerprint);
        return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, fallbackSocketPath);
    }

    /// <summary> Resolves the GUI supervisor transport endpoint for the given project identity. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The resolved GUI supervisor transport endpoint. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectFingerprint" /> is <see langword="null" />. </exception>
    public static IpcEndpoint ResolveGuiSupervisorEndpoint (
        string storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        if (projectFingerprint == null)
        {
            throw new ArgumentNullException(nameof(projectFingerprint));
        }

        var normalizedStorageRoot = UcliStoragePathResolver.NormalizeStorageRootPath(storageRoot);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pipeName = UcliIpcEndpointNames.GuiSupervisorAddressPrefix + projectFingerprint;
            return new IpcEndpoint(IpcTransportKind.NamedPipe, pipeName);
        }

        var preferredSocketPath = Path.Combine(
            UcliStoragePathResolver.ResolveFingerprintDirectory(normalizedStorageRoot, projectFingerprint),
            "gui-supervisor.sock");

        if (Encoding.UTF8.GetByteCount(preferredSocketPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, preferredSocketPath);
        }

        var fallbackSocketPath = UnixSocketPathUtilities.BuildFallbackSocketPath(
            UcliIpcEndpointNames.GuiSupervisorAddressPrefix,
            $"{normalizedStorageRoot}\n{projectFingerprint}");
        return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, fallbackSocketPath);
    }

    private static string BuildFallbackUnixSocketPath (
        string normalizedStorageRoot,
        ProjectFingerprint projectFingerprint)
    {
        if (projectFingerprint == null)
        {
            throw new ArgumentNullException(nameof(projectFingerprint));
        }

        var hashSource = $"{normalizedStorageRoot}\n{projectFingerprint}";
        return UnixSocketPathUtilities.BuildFallbackSocketPath(
            UcliIpcEndpointNames.DaemonAddressPrefix,
            hashSource);
    }
}
