using System.Text;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary> Identifies the canonical Unix-domain socket path eligible for supervisor runtime cleanup. </summary>
internal sealed class SupervisorUnixSocketCleanupTarget
{
    /// <summary> Initializes one validated Unix-domain socket cleanup target. </summary>
    /// <param name="socketPath"> The fully qualified canonical socket path. </param>
    public SupervisorUnixSocketCleanupTarget (AbsolutePath socketPath)
    {
        ArgumentNullException.ThrowIfNull(socketPath);

        if (Encoding.UTF8.GetByteCount(socketPath.Value) > IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            throw new ArgumentException(
                "Supervisor Unix socket cleanup path exceeds the transport path limit.",
                nameof(socketPath));
        }

        SocketPath = socketPath;
    }

    /// <summary> Gets the fully qualified canonical Unix-domain socket path. </summary>
    public AbsolutePath SocketPath { get; }
}
