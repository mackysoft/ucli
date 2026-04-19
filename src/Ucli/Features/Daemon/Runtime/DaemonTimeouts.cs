namespace MackySoft.Ucli.Features.Daemon.Runtime;

/// <summary> Defines internal timeout values shared by daemon workflows. </summary>
internal static class DaemonTimeouts
{
    /// <summary> Gets the timeout cap for one daemon probe attempt. </summary>
    public static readonly TimeSpan ProbeAttemptTimeoutCap = TimeSpan.FromSeconds(1);

    /// <summary> Gets the timeout budget used when launch-failure compensation is enforced. </summary>
    public static readonly TimeSpan LaunchCompensationTimeout = TimeSpan.FromSeconds(10);

    /// <summary> Gets the timeout budget used for stop compensation when main deadline is exhausted. </summary>
    public static readonly TimeSpan StopCompensationTimeout = TimeSpan.FromSeconds(10);

    /// <summary> Gets retry delay for startup readiness probe loops in milliseconds. </summary>
    public const int StartupProbeRetryDelayMilliseconds = 100;

    /// <summary> Gets retry delay for process-termination probe loops in milliseconds. </summary>
    public const int ProcessTerminationProbeRetryDelayMilliseconds = 100;
}