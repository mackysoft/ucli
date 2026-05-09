using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Processes one IPC request through the Unity application pipeline. </summary>
    internal interface IUnityIpcRequestProcessor
    {
        /// <summary> Processes one IPC request and returns the response envelope. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by host execution. </param>
        /// <returns> The processed IPC response envelope. </returns>
        Task<IpcResponse> ProcessAsync (
            IpcRequest request,
            CancellationToken cancellationToken = default);
    }
}