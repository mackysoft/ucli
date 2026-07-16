using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates raw Unity IPC envelopes and dispatches authorized requests. </summary>
    internal interface IUnityIpcRequestHandler
    {
        /// <summary> Authorizes and validates one raw IPC request envelope. </summary>
        /// <param name="request"> The incoming raw IPC request envelope. </param>
        /// <param name="phaseScope"> The connection-owned phase scope for the complete exchange. </param>
        /// <returns> Either the validated endpoint request or its terminal validation error. </returns>
        Task<UnityIpcRequestValidationResult> ValidateAsync (
            IpcRequestEnvelope request,
            IpcRequestPhaseScope phaseScope);

        /// <summary> Handles one validated non-streaming IPC request. </summary>
        /// <param name="request"> The authorized and validated Unity IPC request. </param>
        /// <param name="phaseScope"> The connection-owned phase scope for the complete exchange. </param>
        /// <returns> The response envelope for the request. </returns>
        Task<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestPhaseScope phaseScope);

        /// <summary> Handles one validated streaming IPC request and allows progress frame output. </summary>
        /// <param name="request"> The authorized and validated Unity IPC request. </param>
        /// <param name="streamWriter"> The progress frame writer for the request. </param>
        /// <param name="phaseScope"> The connection-owned phase scope for the complete exchange. </param>
        /// <returns> The terminal response envelope for the request. </returns>
        Task<IpcResponse> HandleStreamingAsync (
            ValidatedUnityIpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            IpcRequestPhaseScope phaseScope);
    }
}
