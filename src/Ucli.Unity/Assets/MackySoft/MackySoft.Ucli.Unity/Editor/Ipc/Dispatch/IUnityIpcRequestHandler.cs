using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles one authenticated IPC request and returns the response envelope. </summary>
    internal interface IUnityIpcRequestHandler
    {
        /// <summary> Handles one IPC request envelope. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The response envelope for the request. </returns>
        Task<IpcResponse> HandleAsync (
            IpcRequest request,
            CancellationToken cancellationToken = default);

        /// <summary> Handles one IPC request envelope and allows progress frame output. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="streamWriter"> The progress frame writer for the request. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The terminal response envelope for the request. </returns>
        Task<IpcResponse> HandleStreamingAsync (
            IpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken = default);
    }
}
