namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubDaemonReachabilityClassifier : IDaemonReachabilityClassifier
{
    private readonly Func<Exception, bool> isNotRunning;

    public StubDaemonReachabilityClassifier (Func<Exception, bool> isNotRunning)
    {
        this.isNotRunning = isNotRunning;
    }

    public bool IsNotRunning (Exception exception)
    {
        return isNotRunning(exception);
    }

    public bool IsSessionTokenInvalid (Exception exception)
    {
        return false;
    }
}
