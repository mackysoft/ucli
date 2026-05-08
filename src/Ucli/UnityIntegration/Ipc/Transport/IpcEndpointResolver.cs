using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Resolves daemon IPC endpoints from project identity values. </summary>
internal sealed class IpcEndpointResolver : IIpcEndpointResolver
{
    /// <summary> Resolves the transport endpoint for the given project identity. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The resolved transport endpoint. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />, empty, or whitespace. </exception>
    public IpcEndpoint Resolve (
        string storageRoot,
        string projectFingerprint)
    {
        return UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, projectFingerprint);
    }
}
