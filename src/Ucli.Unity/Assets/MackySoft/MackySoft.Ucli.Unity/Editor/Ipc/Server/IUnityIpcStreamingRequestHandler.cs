using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles authenticated IPC requests with streaming progress output. </summary>
    internal interface IUnityIpcStreamingRequestHandler
    {
        /// <summary> Handles one streaming IPC request envelope. </summary>
        Task<IpcResponse> HandleStreamingAsync (
            IpcRequest request,
            IUnityIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken = default);
    }
}
