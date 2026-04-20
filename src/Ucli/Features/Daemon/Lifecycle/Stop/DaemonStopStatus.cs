namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;

/// <summary> Defines daemon stop operation outcomes. </summary>
internal enum DaemonStopStatus
{
    Stopped = 0,
    NotRunning = 1,
    Failed = 2,
}