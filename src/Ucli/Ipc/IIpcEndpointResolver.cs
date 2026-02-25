namespace MackySoft.Ucli.Ipc;

/// <summary> Resolves transport-specific IPC endpoint values from project context. </summary>
internal interface IIpcEndpointResolver
{
    /// <summary> Resolves the endpoint for a Unity project daemon. </summary>
    /// <param name="projectRoot"> The absolute or relative Unity project root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The resolved project fingerprint. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The resolved IPC endpoint. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />, empty, or whitespace. </exception>
    IpcEndpoint Resolve (
        string projectRoot,
        string projectFingerprint);
}
