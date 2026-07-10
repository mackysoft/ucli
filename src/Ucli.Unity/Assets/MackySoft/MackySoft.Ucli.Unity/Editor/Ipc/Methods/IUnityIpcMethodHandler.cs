using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles one IPC method contract resolved by dispatcher. </summary>
    internal interface IUnityIpcMethodHandler
    {
        /// <summary> Gets the IPC method name this handler supports. </summary>
        string Method { get; }

        /// <summary> Handles one IPC request for <see cref="Method" />. </summary>
        /// <param name="request"> The incoming request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by dispatcher. </param>
        /// <returns> The response envelope. </returns>
        ValueTask<IpcResponse> HandleAsync (
            IpcRequest request,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Marks an IPC method that must remain available independently of the exclusive Unity mutation lane.
    /// </summary>
    internal interface IUnityControlPlaneIpcMethodHandler : IUnityIpcMethodHandler
    {
    }
}
