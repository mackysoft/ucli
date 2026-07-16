using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Dispatches one authorized IPC request to method-specific handlers. </summary>
    internal interface IUnityIpcMethodDispatcher
    {
        /// <summary> Dispatches one validated IPC request by method contract. </summary>
        /// <param name="request"> The authorized and validated Unity IPC request. </param>
        /// <param name="phaseScope"> The connection-owned phase scope for the complete exchange. </param>
        /// <returns> The response envelope for the request. </returns>
        Task<IpcResponse> DispatchAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestPhaseScope phaseScope);

        /// <summary> Dispatches one validated IPC request by method contract and allows progress frame output. </summary>
        /// <param name="request"> The authorized and validated Unity IPC request. </param>
        /// <param name="streamWriter"> The progress frame writer for the request. </param>
        /// <param name="phaseScope"> The connection-owned phase scope for the complete exchange. </param>
        /// <returns> The terminal response envelope for the request. </returns>
        Task<IpcResponse> DispatchStreamingAsync (
            ValidatedUnityIpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            IpcRequestPhaseScope phaseScope);
    }
}
