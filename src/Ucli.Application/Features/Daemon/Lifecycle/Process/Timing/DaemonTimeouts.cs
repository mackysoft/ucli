namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;

/// <summary> Defines internal timeout values shared by daemon workflows. </summary>
internal static class DaemonTimeouts
{
    /// <summary> Gets the timeout cap for one daemon probe attempt. </summary>
    public static readonly TimeSpan ProbeAttemptTimeoutCap = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets the retry window for observing a new session registration after a listener rejects the persisted token.
    /// </summary>
    public static readonly TimeSpan SessionPublicationRetryTimeout = TimeSpan.FromSeconds(2);

    /// <summary> Gets the timeout budget used when launch-failure compensation is enforced. </summary>
    public static readonly TimeSpan LaunchCompensationTimeout = TimeSpan.FromSeconds(10);

    /// <summary> Gets the timeout budget used for stop compensation when main deadline is exhausted. </summary>
    public static readonly TimeSpan StopCompensationTimeout = TimeSpan.FromSeconds(10);

    /// <summary> Gets the independent timeout budget for auxiliary lifecycle metadata persistence. </summary>
    public static readonly TimeSpan SupplementalPersistenceTimeout = TimeSpan.FromSeconds(1);

    /// <summary> Gets retry delay for startup readiness probe loops in milliseconds. </summary>
    public const int StartupProbeRetryDelayMilliseconds = 100;

    /// <summary> Gets retry delay for process-termination probe loops in milliseconds. </summary>
    public const int ProcessTerminationProbeRetryDelayMilliseconds = 100;
}
