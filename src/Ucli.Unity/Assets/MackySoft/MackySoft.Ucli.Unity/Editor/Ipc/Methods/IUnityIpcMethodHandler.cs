using System;
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
    /// Represents one immutable, method-validated terminal response retained across outward mutation-lane cancellation.
    /// </summary>
    internal sealed class UnityExecutionDeadlineResponse
    {
        /// <summary> Initializes a response previously validated by its owning method handler. </summary>
        /// <param name="response"> The validated terminal response. </param>
        public UnityExecutionDeadlineResponse (IpcResponse response)
        {
            Response = response ?? throw new ArgumentNullException(nameof(response));
        }

        /// <summary> Gets the method-validated terminal response. </summary>
        public IpcResponse Response { get; }
    }

    /// <summary>
    /// Lets a method opt into retaining one method-specific deadline response while its mutation lane completes
    /// outward cancellation.
    /// </summary>
    internal interface IUnityExecutionDeadlineResponseProvider
    {
        /// <summary> Gets the method-validated deadline response published before the mutation activity reached its safe state. </summary>
        bool TryGetPublishedExecutionDeadlineResponse (Guid requestId, out UnityExecutionDeadlineResponse? response);

        /// <summary>
        /// Waits until durable admission publishes a deadline response or ends without establishing admission.
        /// </summary>
        Task<UnityExecutionDeadlineResponse?> WaitForExecutionDeadlineResponseAsync (Guid requestId);

        /// <summary> Releases the terminal-response boundary after dispatch has returned or failed. </summary>
        void CompleteExecutionDeadlineResponse (Guid requestId);
    }

    /// <summary>
    /// Marks an IPC method that must remain available independently of the exclusive Unity mutation lane.
    /// </summary>
    internal interface IUnityControlPlaneIpcMethodHandler : IUnityIpcMethodHandler
    {
    }
}
