namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;

/// <summary> Defines daemon start operation outcomes. </summary>
internal enum DaemonStartStatus
{
    Started = 0,
    AlreadyRunning = 1,
    Failed = 2,
    Attached = 3,
}
