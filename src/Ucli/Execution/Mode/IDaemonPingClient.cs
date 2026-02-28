using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution;

/// <summary> Sends one daemon ping request for reachability probing. </summary>
internal interface IDaemonPingClient
{
    /// <summary> Sends one ping request to daemon for the specified project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when daemon responds. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    ValueTask PingAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
