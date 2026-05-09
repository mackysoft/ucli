using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

/// <summary> Sends Unity-log read queries over Unity IPC transport. </summary>
internal interface IUnityLogsClient
{
    /// <summary> Sends one Unity-log read query and returns decoded IPC payload or structured error. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="query"> The Unity-log query values. </param>
    /// <param name="timeout"> The IPC timeout used by the request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
    /// <returns> The Unity-log read attempt result. </returns>
    ValueTask<UnityLogsClientReadResult> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        IpcUnityLogsReadRequest query,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
