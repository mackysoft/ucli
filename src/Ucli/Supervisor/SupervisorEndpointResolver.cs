using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Ipc;

namespace MackySoft.Ucli.Supervisor;

/// <summary> Resolves transport endpoints for the worktree-local supervisor runtime. </summary>
internal sealed class SupervisorEndpointResolver
{
    private const string SocketFileName = "ipc.sock";

    private const string PipeNamePrefix = "ucli-supervisor-";

    private const string UnixSocketFallbackDirectoryPath = "/tmp";

    private const string UnixSocketFallbackFilePrefix = "ucli-supervisor-";

    private const string UnixSocketFallbackFileExtension = ".sock";

    /// <summary> Resolves one transport endpoint for the specified storage root. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The resolved transport endpoint. </returns>
    public IpcEndpoint Resolve (string storageRoot)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        var normalizedStorageRoot = Path.GetFullPath(storageRoot);

        if (OperatingSystem.IsWindows())
        {
            return new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                PipeNamePrefix + ComputeIdentityHash(normalizedStorageRoot)[..24]);
        }

        var preferredSocketPath = Path.Combine(
            UcliStoragePathResolver.ResolveSupervisorDirectoryPath(normalizedStorageRoot),
            SocketFileName);
        if (Encoding.UTF8.GetByteCount(preferredSocketPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, preferredSocketPath);
        }

        var shortHash = ComputeIdentityHash(normalizedStorageRoot)[..32];
        return new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            Path.Combine(
                UnixSocketFallbackDirectoryPath,
                UnixSocketFallbackFilePrefix + shortHash + UnixSocketFallbackFileExtension));
    }

    private static string ComputeIdentityHash (string normalizedStorageRoot)
    {
        return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(normalizedStorageRoot));
    }
}