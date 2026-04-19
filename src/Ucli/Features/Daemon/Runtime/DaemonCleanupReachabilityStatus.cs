namespace MackySoft.Ucli.Features.Daemon.Runtime;

/// <summary> Represents cleanup-specific reachability assessment for one daemon endpoint. </summary>
internal enum DaemonCleanupReachabilityStatus
{
    NotRunning = 0,
    Running = 1,
    Uncertain = 2,
    Failed = 3,
}