using System.Text;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary> Identifies the canonical Unix-domain socket path eligible for supervisor runtime cleanup. </summary>
internal sealed class SupervisorUnixSocketCleanupTarget
{
    /// <summary> Initializes one validated Unix-domain socket cleanup target. </summary>
    /// <param name="socketPath"> The fully qualified canonical socket path. </param>
    public SupervisorUnixSocketCleanupTarget (string socketPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        if (!PathNormalizer.IsFullyQualifiedPath(socketPath))
        {
            throw new ArgumentException("Supervisor Unix socket cleanup path must be fully qualified.", nameof(socketPath));
        }

        var pathResult = PathNormalizer.TryNormalizeFullPath(socketPath);
        if (!pathResult.IsSuccess)
        {
            throw new ArgumentException(pathResult.DiagnosticMessage, nameof(socketPath));
        }

        if (Encoding.UTF8.GetByteCount(pathResult.FullPath!) > IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            throw new ArgumentException(
                "Supervisor Unix socket cleanup path exceeds the transport path limit.",
                nameof(socketPath));
        }

        SocketPath = pathResult.FullPath!;
    }

    /// <summary> Gets the fully qualified canonical Unix-domain socket path. </summary>
    public string SocketPath { get; }
}
