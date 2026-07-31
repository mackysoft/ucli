using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
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
    /// <param name="deadline">
    /// The caller's wait and IPC-delivery deadline. A Lifecycle Execution carries its distinct immutable
    /// execution deadline in <paramref name="dispatchRequest" />.
    /// </param>
    /// <param name="cancellationToken">
    /// The caller's wait cancellation. After a Lifecycle Execution start becomes durable, cancellation
    /// stops response waiting without canceling provider delivery or execution.
    /// </param>
    /// <returns>
    /// The execution result that contains either one response envelope or one classified failure.
    /// A canceled Lifecycle Execution wait retains its provider-confirmed start binding.
    /// </returns>
    ValueTask<UnityRequestExecutionResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to reconnect through an already-existing provider endpoint that proves ownership
    /// of <paramref name="requiredStart" />. This method must not launch or select another Unity host.
    /// </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="dispatchRequest"> The Lifecycle Execution action dispatch. </param>
    /// <param name="requiredStart"> The authoritative persisted start binding. </param>
    /// <param name="deadline"> The caller's wait and IPC-delivery deadline. </param>
    /// <param name="cancellationToken"> The caller's wait cancellation. </param>
    /// <returns>
    /// An owned result only after the provider has proved the original host; otherwise a
    /// non-owned attempt so another existing provider may be inspected.
    /// </returns>
    ValueTask<UnityIpcReconnectAttempt> TryReconnectAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        LifecycleExecutionStartBinding requiredStart,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);

    /// <summary> Sends one request and reads progress frames until the terminal response frame is received. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="dispatchRequest"> The IPC dispatch request. </param>
    /// <param name="deadline">
    /// The caller's wait and IPC-delivery deadline. A Lifecycle Execution carries its distinct immutable
    /// execution deadline in <paramref name="dispatchRequest" />.
    /// </param>
    /// <param name="onProgressFrame"> The callback invoked for each progress frame. </param>
    /// <param name="cancellationToken">
    /// The caller's wait cancellation. After a Lifecycle Execution start becomes durable, cancellation
    /// stops response waiting without canceling provider delivery or execution.
    /// </param>
    /// <returns>
    /// The execution result that contains either one terminal response envelope or one classified failure.
    /// A canceled Lifecycle Execution wait retains its provider-confirmed start binding.
    /// </returns>
    ValueTask<UnityRequestExecutionResult> SendStreamingAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default);
}
