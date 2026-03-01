namespace MackySoft.Ucli.Daemon;

/// <summary> Defines daemon start operation outcomes. </summary>
internal enum DaemonStartStatus
{
    Started = 0,
    AlreadyRunning = 1,
    Failed = 2,
}