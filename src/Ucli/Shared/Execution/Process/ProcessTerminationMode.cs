namespace MackySoft.Ucli.Shared.Execution.Process;

/// <summary> Defines how a timed-out or canceled process should be terminated. </summary>
internal enum ProcessTerminationMode
{
    /// <summary> Immediately force-kills the process tree. </summary>
    ForceKill,

    /// <summary> Requests graceful exit first, then force-kills the process tree when it does not exit in time. </summary>
    GracefulThenKill,
}
