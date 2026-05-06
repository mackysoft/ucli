using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary> Sends one IPC request through one resolved Unity execution target. </summary>
internal interface IUnityIpcClient
{
    /// <summary> Gets the execution target served by this client. </summary>
    UnityExecutionTarget Target { get; }

    /// <summary> Sends one request through the configured Unity execution target. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="dispatchRequest"> The IPC dispatch request. </param>
    /// <param name="timeout"> The timeout applied to the request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The execution result that contains either one response envelope or one classified failure. </returns>
    ValueTask<UnityRequestExecutionResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
