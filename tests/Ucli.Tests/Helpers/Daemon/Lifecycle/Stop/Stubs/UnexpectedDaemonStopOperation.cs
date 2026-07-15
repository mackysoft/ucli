using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class UnexpectedDaemonStopOperation : IDaemonStopOperation
{
    private readonly string reason;

    public UnexpectedDaemonStopOperation (string reason)
    {
        this.reason = reason;
    }

    public ValueTask<DaemonStopResult> StopAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }
}
