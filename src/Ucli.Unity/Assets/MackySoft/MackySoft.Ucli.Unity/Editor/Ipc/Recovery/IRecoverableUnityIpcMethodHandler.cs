using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles one IPC method with recoverable operation context support. </summary>
    internal interface IRecoverableUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        /// <summary> Handles one IPC request with access to persisted recoverable operation state. </summary>
        ValueTask<IpcResponse> HandleRecoverableAsync (
            IpcRequest request,
            RecoverableIpcOperationContext context,
            CancellationToken cancellationToken);
    }
}
