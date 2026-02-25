namespace MackySoft.Ucli.Ipc;

/// <summary> Resolves daemon IPC endpoints from project identity values. </summary>
internal sealed class IpcEndpointResolver : IIpcEndpointResolver
{
    private const string UcliDirectoryName = ".ucli";

    private const string LocalDirectoryName = "local";

    private const string SocketFileName = "ipc.sock";

    private const string PipeNamePrefix = "ucli-";

    /// <summary> Resolves the transport endpoint for the given project identity. </summary>
    /// <param name="projectRoot"> The Unity project root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The resolved transport endpoint. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />, empty, or whitespace. </exception>
    public IpcEndpoint Resolve (
        string projectRoot,
        string projectFingerprint)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException("Project root must not be empty.", nameof(projectRoot));
        }

        if (string.IsNullOrWhiteSpace(projectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }

        var normalizedProjectRoot = Path.GetFullPath(projectRoot);
        var normalizedProjectFingerprint = projectFingerprint.Trim();

        if (OperatingSystem.IsWindows())
        {
            var pipeName = PipeNamePrefix + normalizedProjectFingerprint;
            return new IpcEndpoint(IpcTransportKind.NamedPipe, pipeName);
        }

        var socketPath = Path.Combine(
            normalizedProjectRoot,
            UcliDirectoryName,
            LocalDirectoryName,
            normalizedProjectFingerprint,
            SocketFileName);
        return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, socketPath);
    }
}
