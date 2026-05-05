using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Represents the result of probing daemon reachability. </summary>
/// <param name="IsRunning"> Whether daemon endpoint is reachable. </param>
/// <param name="Error"> The infrastructure error from probing; otherwise <see langword="null" />. </param>
internal sealed record DaemonReachabilityProbeResult (
    bool IsRunning,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether probing failed with an infrastructure error. </summary>
    public bool HasError => Error is not null;

    /// <summary> Creates a successful probe result that indicates daemon is reachable. </summary>
    /// <returns> The successful running result. </returns>
    public static DaemonReachabilityProbeResult Running ()
    {
        return new DaemonReachabilityProbeResult(true, null);
    }

    /// <summary> Creates a successful probe result that indicates daemon is not reachable. </summary>
    /// <returns> The successful not-running result. </returns>
    public static DaemonReachabilityProbeResult NotRunning ()
    {
        return new DaemonReachabilityProbeResult(false, null);
    }

    /// <summary> Creates a failed probe result with an infrastructure error. </summary>
    /// <param name="error"> The infrastructure error. </param>
    /// <returns> The failed probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonReachabilityProbeResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonReachabilityProbeResult(false, error);
    }
}
