using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Represents cleanup-specific daemon reachability probing result. </summary>
/// <param name="Status"> The cleanup-specific reachability status. </param>
/// <param name="Error"> The structured error when probing failed; otherwise <see langword="null" />. </param>
/// <param name="UncertainReason"> The known ambiguity reason when status is <see cref="DaemonCleanupReachabilityStatus.Uncertain" />; otherwise <see langword="null" />. </param>
internal sealed record DaemonCleanupReachabilityProbeResult (
    DaemonCleanupReachabilityStatus Status,
    ExecutionError? Error,
    DaemonCleanupReachabilityUncertainReason? UncertainReason)
{
    /// <summary> Creates a successful probe result that proves the canonical endpoint is absent strongly enough for destructive cleanup. </summary>
    /// <returns> The successful not-running result. </returns>
    public static DaemonCleanupReachabilityProbeResult NotRunning ()
    {
        return new DaemonCleanupReachabilityProbeResult(DaemonCleanupReachabilityStatus.NotRunning, null, null);
    }

    /// <summary> Creates a successful probe result that proves the daemon accepted the ping successfully. </summary>
    /// <returns> The successful running result. </returns>
    public static DaemonCleanupReachabilityProbeResult Running ()
    {
        return new DaemonCleanupReachabilityProbeResult(DaemonCleanupReachabilityStatus.Running, null, null);
    }

    /// <summary> Creates a successful probe result that cannot safely prove canonical endpoint absence. </summary>
    /// <param name="reason"> The known ambiguity reason. </param>
    /// <returns> The successful uncertain result. </returns>
    public static DaemonCleanupReachabilityProbeResult Uncertain (DaemonCleanupReachabilityUncertainReason reason)
    {
        return new DaemonCleanupReachabilityProbeResult(DaemonCleanupReachabilityStatus.Uncertain, null, reason);
    }

    /// <summary> Creates a failed probe result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonCleanupReachabilityProbeResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonCleanupReachabilityProbeResult(DaemonCleanupReachabilityStatus.Failed, error, null);
    }
}