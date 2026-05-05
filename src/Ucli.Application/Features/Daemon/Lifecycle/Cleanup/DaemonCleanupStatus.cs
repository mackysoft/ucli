namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Defines daemon cleanup outcome states. </summary>
internal enum DaemonCleanupStatus
{
    /// <summary> Indicates cleanup completed successfully. </summary>
    Completed = 0,

    /// <summary> Indicates cleanup was intentionally skipped for safety. </summary>
    Skipped = 1,

    /// <summary> Indicates cleanup failed. </summary>
    Failed = 2,
}
