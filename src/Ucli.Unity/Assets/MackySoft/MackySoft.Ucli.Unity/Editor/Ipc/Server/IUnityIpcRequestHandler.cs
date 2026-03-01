using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles one authenticated IPC request and returns the response envelope. </summary>
    internal interface IUnityIpcRequestHandler
    {
        /// <summary> Handles one IPC request envelope. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The response envelope for the request. </returns>
        Task<IpcResponse> Handle (
            IpcRequest request,
            CancellationToken cancellationToken = default);
    }
}
