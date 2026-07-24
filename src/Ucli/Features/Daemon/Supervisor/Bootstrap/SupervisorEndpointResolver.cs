using System.Text;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;

/// <summary> Resolves transport endpoints for the worktree-local supervisor runtime. </summary>
internal sealed class SupervisorEndpointResolver
{
    /// <summary> Resolves the canonical Unix-domain socket eligible for filesystem cleanup. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The Unix-domain socket cleanup target, or <see langword="null" /> when the current platform uses named pipes. </returns>
    public SupervisorUnixSocketCleanupTarget? ResolveUnixSocketCleanupTargetOrNull (AbsolutePath storageRoot)
    {
        return OperatingSystem.IsWindows()
            ? null
            : new SupervisorUnixSocketCleanupTarget(ResolveUnixSocketPath(storageRoot));
    }

    /// <summary> Resolves one listener endpoint for a specific supervisor generation. </summary>
    public SupervisorTransportEndpoint ResolveRuntimeEndpoint (
        AbsolutePath storageRoot,
        IpcSessionToken sessionToken)
    {
        ArgumentNullException.ThrowIfNull(sessionToken);
        return OperatingSystem.IsWindows()
            ? SupervisorTransportEndpoint.FromNamedPipeAddress(
                CreateNamedPipeGenerationAddress(storageRoot, sessionToken))
            : SupervisorTransportEndpoint.FromUnixSocketPath(
                ResolveUnixSocketPath(storageRoot));
    }

    internal static string CreateNamedPipeGenerationAddress (
        AbsolutePath storageRoot,
        IpcSessionToken sessionToken)
    {
        ArgumentNullException.ThrowIfNull(sessionToken);
        var worktreeIdentity = SupervisorWorktreeIdentity.Create(storageRoot);
        var generationHash = Sha256Digest.Compute(
            Encoding.UTF8.GetBytes(sessionToken.GetEncodedValue())).ToString()[..12];
        return $"{UcliIpcEndpointNames.SupervisorAddressPrefix}{worktreeIdentity.NamedPipeAddressSegment}-{generationHash}";
    }

    private static AbsolutePath ResolveUnixSocketPath (AbsolutePath storageRoot)
    {
        var preferredSocketPath = ContainedPath.Create(
            UcliStoragePathResolver.ResolveSupervisorDirectoryPath(storageRoot),
            RootRelativePath.Parse(UcliIpcEndpointNames.UnixSocketFileName)).Target;
        return Encoding.UTF8.GetByteCount(preferredSocketPath.Value) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes
            ? preferredSocketPath
            : new UnixSocketFallbackPath(
                AbsolutePath.Parse(Path.GetTempPath()),
                UnixSocketFallbackPurpose.Supervisor,
                storageRoot.Value).SocketPath;
    }
}
