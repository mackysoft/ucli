using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles one IPC method contract resolved by dispatcher. </summary>
    internal interface IUnityIpcMethodHandler
    {
        /// <summary> Gets the defined Unity IPC method this handler supports. </summary>
        UnityIpcMethod Method { get; }

        /// <summary> Handles one IPC request for <see cref="Method" />. </summary>
        /// <param name="request"> The incoming request envelope. </param>
        /// <param name="cancellation"> The request cancellation state owned by dispatcher. </param>
        /// <returns> The response envelope. </returns>
        ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation);
    }

    /// <summary>
    /// Marks an IPC method that must remain available independently of the exclusive Unity mutation lane.
    /// </summary>
    internal interface IUnityControlPlaneIpcMethodHandler : IUnityIpcMethodHandler
    {
    }
}
