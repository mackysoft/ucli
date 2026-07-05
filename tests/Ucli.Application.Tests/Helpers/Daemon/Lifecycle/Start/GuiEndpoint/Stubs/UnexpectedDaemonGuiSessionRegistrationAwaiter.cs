namespace MackySoft.Ucli.Application.Tests;

internal sealed class UnexpectedDaemonGuiSessionRegistrationAwaiter : IDaemonGuiSessionRegistrationAwaiter
{
    private readonly string reason;

    public UnexpectedDaemonGuiSessionRegistrationAwaiter (string reason)
    {
        this.reason = reason;
    }

    public ValueTask<DaemonGuiSessionRegistrationWaitResult> WaitForSessionAsync (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        TimeSpan timeout,
        DateTimeOffset? expectedProcessStartedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }
}
