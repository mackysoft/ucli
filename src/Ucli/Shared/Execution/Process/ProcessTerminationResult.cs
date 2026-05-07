namespace MackySoft.Ucli.Shared.Execution.Process;

/// <summary> Represents how process termination completed after timeout or cleanup. </summary>
internal enum ProcessTerminationResult
{
    /// <summary> No termination was requested because the process had already exited or never started. </summary>
    None,

    /// <summary> The process exited after a graceful termination request. </summary>
    GracefulExited,

    /// <summary> Force kill was requested and no immediate kill failure was observed. </summary>
    ForceKilled,

    /// <summary> The process could not be confirmed stopped after force kill. </summary>
    ForceKillFailed,
}
