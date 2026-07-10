namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

/// <summary> Queries daemon lifecycle status for one Unity project context. </summary>
internal interface IDaemonStatusOperation
{
    /// <summary> Gets daemon status for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon status timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon status result, including the observed ping payload for a running daemon. </returns>
    ValueTask<DaemonStatusResult> GetStatusAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
