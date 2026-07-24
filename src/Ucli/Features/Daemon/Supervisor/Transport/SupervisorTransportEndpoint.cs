using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary>
/// Carries a supervisor IPC endpoint while retaining a guarded path only for Unix-domain sockets.
/// </summary>
internal sealed record SupervisorTransportEndpoint
{
    private SupervisorTransportEndpoint (IpcTransportEndpoint runtimeEndpoint)
    {
        RuntimeEndpoint = runtimeEndpoint;
    }

    /// <summary> Gets the IPC representation used at transport and persistence boundaries. </summary>
    public IpcEndpoint Contract => RuntimeEndpoint.Contract;

    /// <summary>
    /// Gets the guarded Unix-domain socket path, or <see langword="null" /> for a Named Pipe endpoint.
    /// </summary>
    public MackySoft.FileSystem.AbsolutePath? UnixSocketPath => RuntimeEndpoint.UnixSocketPath;

    /// <summary> Gets the guarded runtime transport endpoint used by internal IPC clients. </summary>
    public IpcTransportEndpoint RuntimeEndpoint { get; }

    /// <summary> Creates a Named Pipe endpoint without interpreting its address as a filesystem path. </summary>
    public static SupervisorTransportEndpoint FromNamedPipeAddress (string address)
    {
        return new SupervisorTransportEndpoint(IpcTransportEndpoint.FromNamedPipeAddress(address));
    }

    /// <summary> Creates a Unix-domain socket endpoint from a guarded absolute path. </summary>
    public static SupervisorTransportEndpoint FromUnixSocketPath (MackySoft.FileSystem.AbsolutePath socketPath)
    {
        return new SupervisorTransportEndpoint(IpcTransportEndpoint.FromUnixSocketPath(socketPath));
    }

    /// <summary>
    /// Converts one IPC endpoint at the manifest boundary, parsing Unix socket text exactly once.
    /// </summary>
    public static SupervisorTransportEndpoint FromContract (IpcEndpoint contract)
    {
        return new SupervisorTransportEndpoint(IpcTransportEndpoint.FromContract(contract));
    }
}
