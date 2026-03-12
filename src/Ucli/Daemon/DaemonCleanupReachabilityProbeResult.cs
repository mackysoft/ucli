using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Represents cleanup-specific daemon reachability probing result. </summary>
/// <param name="Status"> The cleanup-specific reachability status. </param>
/// <param name="Error"> The structured error when probing failed; otherwise <see langword="null" />. </param>
internal sealed record DaemonCleanupReachabilityProbeResult (
    DaemonCleanupReachabilityStatus Status,
    ExecutionError? Error)
{
    /// <summary> Creates a successful probe result that proves daemon is not running. </summary>
    /// <returns> The successful not-running result. </returns>
    public static DaemonCleanupReachabilityProbeResult NotRunning ()
    {
        return new DaemonCleanupReachabilityProbeResult(DaemonCleanupReachabilityStatus.NotRunning, null);
    }

    /// <summary> Creates a successful probe result that proves daemon is running. </summary>
    /// <returns> The successful running result. </returns>
    public static DaemonCleanupReachabilityProbeResult Running ()
    {
        return new DaemonCleanupReachabilityProbeResult(DaemonCleanupReachabilityStatus.Running, null);
    }

    /// <summary> Creates a successful probe result that cannot safely prove daemon liveness. </summary>
    /// <returns> The successful uncertain result. </returns>
    public static DaemonCleanupReachabilityProbeResult Uncertain ()
    {
        return new DaemonCleanupReachabilityProbeResult(DaemonCleanupReachabilityStatus.Uncertain, null);
    }

    /// <summary> Creates a failed probe result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonCleanupReachabilityProbeResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonCleanupReachabilityProbeResult(DaemonCleanupReachabilityStatus.Failed, error);
    }
}