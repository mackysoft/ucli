using System.Runtime.InteropServices;
using System.Text;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Resolves shared uCLI IPC endpoints from storage and project identity values. </summary>
internal static class UcliIpcEndpointResolver
{
    /// <summary> Resolves the daemon transport endpoint for the given project identity. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The resolved daemon transport endpoint. </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="storageRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />.
    /// </exception>
    public static IpcTransportEndpoint ResolveDaemonEndpoint (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        var unixSocketPath = ResolveDaemonUnixSocketPathOrNull(storageRoot, projectFingerprint);
        if (unixSocketPath is null)
        {
            var pipeName = UcliIpcEndpointNames.DaemonAddressPrefix + projectFingerprint;
            return IpcTransportEndpoint.FromNamedPipeAddress(pipeName);
        }

        return IpcTransportEndpoint.FromUnixSocketPath(unixSocketPath);
    }

    /// <summary> Resolves the guarded daemon Unix-domain socket path used by filesystem operations. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns>
    /// The guarded Unix-domain socket path, or <see langword="null" /> when the current platform uses named pipes.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="storageRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />.
    /// </exception>
    public static AbsolutePath? ResolveDaemonUnixSocketPathOrNull (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        if (storageRoot == null)
        {
            throw new ArgumentNullException(nameof(storageRoot));
        }

        if (projectFingerprint == null)
        {
            throw new ArgumentNullException(nameof(projectFingerprint));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        var preferredSocketPath = ContainedPath.Create(
            UcliStoragePathResolver.ResolveProjectDirectory(storageRoot, projectFingerprint),
            RootRelativePath.Parse(UcliIpcEndpointNames.UnixSocketFileName)).Target;

        if (Encoding.UTF8.GetByteCount(preferredSocketPath.Value) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            return preferredSocketPath;
        }

        var fallbackPath = new UnixSocketFallbackPath(
            AbsolutePath.Parse(Path.GetTempPath()),
            UnixSocketFallbackPurpose.Daemon,
            $"{storageRoot}\n{projectFingerprint}");
        return fallbackPath.SocketPath;
    }

    /// <summary> Resolves the GUI supervisor transport endpoint for the given project identity. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <returns> The resolved GUI supervisor transport endpoint. </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="storageRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />.
    /// </exception>
    public static IpcTransportEndpoint ResolveGuiSupervisorEndpoint (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        if (storageRoot == null)
        {
            throw new ArgumentNullException(nameof(storageRoot));
        }

        if (projectFingerprint == null)
        {
            throw new ArgumentNullException(nameof(projectFingerprint));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pipeName = UcliIpcEndpointNames.GuiSupervisorAddressPrefix + projectFingerprint;
            return IpcTransportEndpoint.FromNamedPipeAddress(pipeName);
        }

        var preferredSocketPath = ContainedPath.Create(
            UcliStoragePathResolver.ResolveProjectDirectory(storageRoot, projectFingerprint),
            RootRelativePath.Parse("gui-supervisor.sock")).Target;

        if (Encoding.UTF8.GetByteCount(preferredSocketPath.Value) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            return IpcTransportEndpoint.FromUnixSocketPath(preferredSocketPath);
        }

        var fallbackPath = new UnixSocketFallbackPath(
            AbsolutePath.Parse(Path.GetTempPath()),
            UnixSocketFallbackPurpose.GuiSupervisor,
            $"{storageRoot}\n{projectFingerprint}");
        return IpcTransportEndpoint.FromUnixSocketPath(fallbackPath.SocketPath);
    }
}
