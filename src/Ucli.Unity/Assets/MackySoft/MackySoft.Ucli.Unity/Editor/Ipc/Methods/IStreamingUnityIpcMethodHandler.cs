using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles one IPC method that can emit streaming progress frames. </summary>
    internal interface IStreamingUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        /// <summary> Handles one streaming IPC request. </summary>
        ValueTask<IpcResponse> HandleStreamingAsync (
            ValidatedUnityIpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            IpcRequestCancellation cancellation);
    }
}
