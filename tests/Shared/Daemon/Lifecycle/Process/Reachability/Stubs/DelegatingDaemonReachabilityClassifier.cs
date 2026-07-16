namespace MackySoft.Ucli.TestSupport;

internal sealed class DelegatingDaemonReachabilityClassifier :
    MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Reachability.IDaemonReachabilityClassifier
{
    private readonly Func<Exception, bool> isNotRunning;

    private readonly Func<Exception, bool> isSessionTokenInvalid;

    private readonly Func<Exception, bool> isRetryableBeforeRequestWrite;

    private readonly Func<Exception, bool> isRequestTimeout;

    private readonly Func<Exception, bool> isRecoverableResponseInterruption;

    public DelegatingDaemonReachabilityClassifier (
        Func<Exception, bool> isNotRunning,
        Func<Exception, bool> isSessionTokenInvalid,
        Func<Exception, bool> isRetryableBeforeRequestWrite,
        Func<Exception, bool> isRequestTimeout,
        Func<Exception, bool> isRecoverableResponseInterruption)
    {
        this.isNotRunning = isNotRunning ?? throw new ArgumentNullException(nameof(isNotRunning));
        this.isSessionTokenInvalid = isSessionTokenInvalid ?? throw new ArgumentNullException(nameof(isSessionTokenInvalid));
        this.isRetryableBeforeRequestWrite = isRetryableBeforeRequestWrite ?? throw new ArgumentNullException(nameof(isRetryableBeforeRequestWrite));
        this.isRequestTimeout = isRequestTimeout ?? throw new ArgumentNullException(nameof(isRequestTimeout));
        this.isRecoverableResponseInterruption = isRecoverableResponseInterruption ?? throw new ArgumentNullException(nameof(isRecoverableResponseInterruption));
    }

    public bool IsNotRunning (Exception exception)
    {
        return isNotRunning(exception);
    }

    public bool IsSessionTokenInvalid (Exception exception)
    {
        return isSessionTokenInvalid(exception);
    }

    public bool IsRetryableBeforeRequestWrite (Exception exception)
    {
        return isRetryableBeforeRequestWrite(exception);
    }

    public bool IsRequestTimeout (Exception exception)
    {
        return isRequestTimeout(exception);
    }

    public bool IsRecoverableResponseInterruption (Exception exception)
    {
        return isRecoverableResponseInterruption(exception);
    }
}
