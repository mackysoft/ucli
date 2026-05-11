namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;

/// <summary> Defines daemon startup retry-disposition values. </summary>
internal static class DaemonStartupRetryDispositionValues
{
    public const string RetryImmediately = "retryImmediately";

    public const string WaitThenRetry = "waitThenRetry";

    public const string RetryAfterFix = "retryAfterFix";

    public const string ManualActionRequired = "manualActionRequired";

    public const string DoNotRetry = "doNotRetry";

    public const string Unknown = "unknown";
}
