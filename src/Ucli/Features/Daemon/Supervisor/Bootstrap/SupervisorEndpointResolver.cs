using System.Text;
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
    public SupervisorUnixSocketCleanupTarget? ResolveUnixSocketCleanupTargetOrNull (string storageRoot)
    {
        var normalizedStorageRoot = NormalizeStorageRoot(storageRoot);
        return OperatingSystem.IsWindows()
            ? null
            : new SupervisorUnixSocketCleanupTarget(ResolveUnixSocketPath(normalizedStorageRoot));
    }

    /// <summary> Resolves one listener endpoint for a specific supervisor generation. </summary>
    public IpcEndpoint ResolveRuntimeEndpoint (
        string storageRoot,
        IpcSessionToken sessionToken)
    {
        ArgumentNullException.ThrowIfNull(sessionToken);
        return OperatingSystem.IsWindows()
            ? new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                CreateNamedPipeGenerationAddress(storageRoot, sessionToken.GetEncodedValue()))
            : new IpcEndpoint(
                IpcTransportKind.UnixDomainSocket,
                ResolveUnixSocketPath(NormalizeStorageRoot(storageRoot)));
    }

    internal static string CreateNamedPipeGenerationAddress (
        string storageRoot,
        string generationIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(generationIdentity);
        var normalizedStorageRoot = UcliStoragePathResolver.NormalizeStorageRootPath(storageRoot);
        var worktreeIdentity = ComputeIdentityHash(normalizedStorageRoot)[..24];
        var generationHash = ComputeIdentityHash(generationIdentity)[..12];
        return $"{UcliIpcEndpointNames.SupervisorAddressPrefix}{worktreeIdentity}-{generationHash}";
    }

    private static string ComputeIdentityHash (string normalizedStorageRoot)
    {
        return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(normalizedStorageRoot));
    }

    private static string NormalizeStorageRoot (string storageRoot)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        return UcliStoragePathResolver.NormalizeStorageRootPath(storageRoot);
    }

    private static string ResolveUnixSocketPath (string normalizedStorageRoot)
    {
        var preferredSocketPath = Path.Combine(
            UcliStoragePathResolver.ResolveSupervisorDirectoryPath(normalizedStorageRoot),
            UcliIpcEndpointNames.UnixSocketFileName);
        return Encoding.UTF8.GetByteCount(preferredSocketPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes
            ? preferredSocketPath
            : new UnixSocketFallbackPath(
                Path.GetTempPath(),
                UnixSocketFallbackPurpose.Supervisor,
                normalizedStorageRoot).SocketPath;
    }
}
