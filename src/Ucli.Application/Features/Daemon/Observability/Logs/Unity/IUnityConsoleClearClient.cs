namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

/// <summary> Sends Unity Editor Console clear requests over Unity IPC transport. </summary>
internal interface IUnityConsoleClearClient
{
    /// <summary> Sends one Unity Editor Console clear request. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The IPC timeout used by the request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
    /// <returns> The clear attempt result. </returns>
    ValueTask<UnityConsoleClearClientResult> ClearAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
