using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed class UnexpectedDaemonPingClient : IDaemonPingClient
{
    private readonly string reason;

    public UnexpectedDaemonPingClient (string reason)
    {
        this.reason = string.IsNullOrWhiteSpace(reason)
            ? "Daemon ping should not be sent."
            : reason;
    }

    public ValueTask PingAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask PingSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask PingCanonicalEndpointWithoutSessionTokenAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask PingCanonicalEndpointWithSessionTokenAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }
}
