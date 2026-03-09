using System.Text.Json;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Ipc;

/// <summary> Sends one IPC request through one resolved Unity execution target. </summary>
internal interface IUnityIpcClient
{
    /// <summary> Sends one request through the configured Unity execution target. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="method"> The IPC method name. </param>
    /// <param name="payload"> The IPC payload element. </param>
    /// <param name="timeout"> The timeout applied to the request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The execution result that contains either one response envelope or one classified failure. </returns>
    ValueTask<UnityIpcRequestExecutionResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        string method,
        JsonElement payload,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}