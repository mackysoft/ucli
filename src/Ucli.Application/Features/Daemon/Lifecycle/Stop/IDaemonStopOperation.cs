namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;

/// <summary> Stops daemon lifecycle for one Unity project context. </summary>
internal interface IDaemonStopOperation
{
    /// <summary> Stops daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The deadline shared by all normal daemon-stop phases. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon stop result. </returns>
    ValueTask<DaemonStopResult> StopAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);
}
