namespace MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;

/// <summary> Represents one classified reachability-probe outcome for the supervisor runtime. </summary>
internal enum SupervisorReachabilityProbeStatus
{
    /// <summary> The supervisor responded with a valid ping payload. </summary>
    Reachable,

    /// <summary> The probe exceeded its request budget before reachability could be confirmed. </summary>
    TimedOut,

    /// <summary> The supervisor endpoint could not be reached or returned an invalid response. </summary>
    Unreachable,
}
