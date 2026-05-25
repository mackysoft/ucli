using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles one IPC method that can emit streaming progress frames. </summary>
    internal interface IStreamingUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        /// <summary> Handles one streaming IPC request. </summary>
        ValueTask<IpcResponse> HandleStreamingAsync (
            IpcRequest request,
            IUnityIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken);
    }
}
