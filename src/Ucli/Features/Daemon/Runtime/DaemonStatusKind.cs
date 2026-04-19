namespace MackySoft.Ucli.Features.Daemon.Runtime;

/// <summary> Defines daemon status query outcomes. </summary>
internal enum DaemonStatusKind
{
    Running = 0,
    NotRunning = 1,
    Stale = 2,
    Failed = 3,
}