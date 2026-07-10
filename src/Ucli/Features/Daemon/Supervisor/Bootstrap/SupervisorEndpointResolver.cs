using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;

/// <summary> Resolves transport endpoints for the worktree-local supervisor runtime. </summary>
internal sealed class SupervisorEndpointResolver
{
    /// <summary> Resolves the canonical endpoint used for filesystem cleanup coordination. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The resolved transport endpoint. </returns>
    public IpcEndpoint ResolveCanonicalEndpoint (string storageRoot)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        var normalizedStorageRoot = UcliStoragePathResolver.NormalizeStorageRootPath(storageRoot);

        if (OperatingSystem.IsWindows())
        {
            return new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                CreateNamedPipeGenerationAddress(normalizedStorageRoot, "canonical"));
        }

        var preferredSocketPath = Path.Combine(
            UcliStoragePathResolver.ResolveSupervisorDirectoryPath(normalizedStorageRoot),
            UcliIpcEndpointNames.UnixSocketFileName);
        if (Encoding.UTF8.GetByteCount(preferredSocketPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, preferredSocketPath);
        }

        return new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            UnixSocketPathUtilities.BuildFallbackSocketPath(
                UcliIpcEndpointNames.SupervisorAddressPrefix,
                normalizedStorageRoot));
    }

    /// <summary> Resolves one listener endpoint for a specific supervisor generation. </summary>
    public IpcEndpoint ResolveRuntimeEndpoint (
        string storageRoot,
        string generationIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(generationIdentity);
        var canonicalEndpoint = ResolveCanonicalEndpoint(storageRoot);
        return canonicalEndpoint.TransportKind == IpcTransportKind.NamedPipe
            ? new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                CreateNamedPipeGenerationAddress(storageRoot, generationIdentity))
            : canonicalEndpoint;
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
}
