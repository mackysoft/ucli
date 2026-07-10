using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal sealed class UnexpectedDaemonPingInfoClient : IDaemonPingInfoClient
{
    private readonly string reason;

    public UnexpectedDaemonPingInfoClient (string reason)
    {
        this.reason = reason;
    }

    public ValueTask<IpcPingResponse> PingAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask<IpcPingResponse> PingSessionAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }
}
