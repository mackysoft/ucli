namespace MackySoft.Ucli.Daemon;

/// <summary> Defines daemon cleanup skip reasons. </summary>
internal enum DaemonCleanupSkipReason
{
    /// <summary> Indicates no skip reason applies. </summary>
    None = 0,

    /// <summary> Indicates cleanup was skipped because daemon is running. </summary>
    Running = 1,

    /// <summary> Indicates cleanup was skipped because invalid session may still belong to a live daemon. </summary>
    UnsafeInvalidSession = 2,

    /// <summary> Indicates cleanup was skipped because reachability could not be determined safely. </summary>
    UncertainReachability = 3,
}