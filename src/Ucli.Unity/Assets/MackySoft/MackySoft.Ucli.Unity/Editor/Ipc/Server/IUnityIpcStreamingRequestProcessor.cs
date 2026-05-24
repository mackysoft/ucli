using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Processes IPC requests that return progress frames before the terminal response. </summary>
    internal interface IUnityIpcStreamingRequestProcessor
    {
        /// <summary> Processes one streaming IPC request and returns the terminal response envelope. </summary>
        Task<IpcResponse> ProcessStreamingAsync (
            IpcRequest request,
            IUnityIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken = default);
    }
}
