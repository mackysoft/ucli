using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;

/// <summary> Stops daemon lifecycle for one Unity project context. </summary>
internal interface IDaemonStopOperation
{
    /// <summary> Stops daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon stop timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon stop result. </returns>
    ValueTask<DaemonStopResult> Stop (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}