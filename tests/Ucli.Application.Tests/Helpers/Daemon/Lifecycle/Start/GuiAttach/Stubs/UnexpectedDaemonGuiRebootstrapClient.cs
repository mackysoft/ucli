namespace MackySoft.Ucli.Application.Tests;

internal sealed class UnexpectedDaemonGuiRebootstrapClient : IDaemonGuiRebootstrapClient
{
    private readonly string reason;

    public UnexpectedDaemonGuiRebootstrapClient (string reason)
    {
        this.reason = reason;
    }

    public ValueTask<DaemonGuiRebootstrapRequestResult> RequestRebootstrapAsync (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        DateTimeOffset? expectedProcessStartedAtUtc,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }
}
