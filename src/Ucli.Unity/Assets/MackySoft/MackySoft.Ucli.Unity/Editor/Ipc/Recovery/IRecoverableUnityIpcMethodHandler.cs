using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles one IPC method with recoverable operation context support. </summary>
    internal interface IRecoverableUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        /// <summary> Tries to create the stable request payload hash used to identify replay-safe recoverable operation state. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="requestPayloadHash"> The stable request payload hash when successful. </param>
        /// <param name="errorResponse"> The error response to return when the request payload cannot produce a recovery identity. </param>
        /// <returns> <see langword="true" /> when the hash was created; otherwise <see langword="false" />. </returns>
        bool TryCreateRecoverableRequestPayloadHash (
            IpcRequest request,
            out string requestPayloadHash,
            out IpcResponse errorResponse);

        /// <summary> Handles one IPC request with access to persisted recoverable operation state. </summary>
        ValueTask<IpcResponse> HandleRecoverableAsync (
            IpcRequest request,
            RecoverableIpcOperationContext context,
            CancellationToken cancellationToken);
    }
}
