using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Represents validated IPC connection values loaded from one daemon session. </summary>
internal sealed record DaemonSessionConnection
{
    /// <summary> Initializes one validated daemon IPC connection. </summary>
    public DaemonSessionConnection (
        IpcSessionToken sessionToken,
        IpcEndpoint endpoint)
    {
        SessionToken = sessionToken ?? throw new ArgumentNullException(nameof(sessionToken));
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }

    /// <summary> Gets the canonical daemon authorization token. </summary>
    public IpcSessionToken SessionToken { get; }

    /// <summary> Gets the daemon IPC endpoint. </summary>
    public IpcEndpoint Endpoint { get; }
}
