using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Ipc;

/// <summary> Sends one IPC request to Unity oneshot batchmode execution and waits for the response. </summary>
internal interface IUnityOneshotIpcClient
{
    /// <summary> Sends one IPC request through Unity oneshot execution. </summary>
    /// <param name="unityProjectRoot"> The target Unity project root path. </param>
    /// <param name="request"> The IPC request envelope to execute. </param>
    /// <param name="timeout"> The timeout applied to one request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The oneshot execution result. </returns>
    ValueTask<UnityIpcRequestExecutionResult> SendAsync (
        string unityProjectRoot,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}