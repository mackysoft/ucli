using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Logs;

/// <summary> Sends daemon-log read queries over Unity IPC transport. </summary>
internal interface IDaemonLogsClient
{
    /// <summary> Sends one daemon-log read query and returns decoded IPC payload or structured error. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="query"> The daemon-log query values. </param>
    /// <param name="timeout"> The IPC timeout used by the request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
    /// <returns> The daemon-log read attempt result. </returns>
    ValueTask<DaemonLogsClientReadResult> Read (
        ResolvedUnityProjectContext unityProject,
        DaemonLogsReadQuery query,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}