namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Specifies whether process-registration release must wait for the registered process to terminate. </summary>
internal enum SupervisorProcessReleaseMode
{
    /// <summary> Waits until the registered process has terminated before completing release. </summary>
    AwaitTermination = 0,

    /// <summary> Requests release without waiting on the currently executing registered process. </summary>
    CurrentProcess = 1,
}
