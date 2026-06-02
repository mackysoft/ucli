using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Dispatches one authorized IPC request to method-specific handlers. </summary>
    internal interface IUnityIpcMethodDispatcher
    {
        /// <summary> Dispatches one IPC request envelope by method contract. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The response envelope for the request. </returns>
        Task<IpcResponse> DispatchAsync (
            IpcRequest request,
            CancellationToken cancellationToken = default);

        /// <summary> Dispatches one IPC request envelope by method contract and allows progress frame output. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="streamWriter"> The progress frame writer for the request. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The terminal response envelope for the request. </returns>
        Task<IpcResponse> DispatchStreamingAsync (
            IpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken = default);
    }
}
