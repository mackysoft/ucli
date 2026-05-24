using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Dispatches authorized IPC requests that support streaming progress output. </summary>
    internal interface IUnityIpcStreamingMethodDispatcher
    {
        /// <summary> Dispatches one streaming IPC request envelope by method contract. </summary>
        Task<IpcResponse> DispatchStreamingAsync (
            IpcRequest request,
            IUnityIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken = default);
    }
}
