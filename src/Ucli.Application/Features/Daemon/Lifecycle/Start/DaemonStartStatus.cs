namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Defines daemon start operation outcomes. </summary>
internal enum DaemonStartStatus
{
    Started = 0,
    AlreadyRunning = 1,
    Failed = 2,
}
