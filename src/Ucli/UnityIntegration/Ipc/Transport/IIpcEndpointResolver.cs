using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Resolves transport-specific IPC endpoint values from project context. </summary>
internal interface IIpcEndpointResolver
{
    /// <summary> Resolves the endpoint for a Unity project daemon. </summary>
    /// <param name="storageRoot"> The absolute or relative storage-root path used to resolve <c>.ucli</c> endpoint paths. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The resolved project fingerprint. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The resolved IPC endpoint. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />, empty, or whitespace. </exception>
    IpcEndpoint Resolve (
        string storageRoot,
        string projectFingerprint);
}