using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Runs one transport-specific accept loop for Unity IPC server. </summary>
    internal interface IUnityIpcTransportListener
    {
        /// <summary> Gets transport kind handled by this listener. </summary>
        IpcTransportKind TransportKind { get; }

        /// <summary> Runs transport-specific accept loop until cancellation is requested. </summary>
        /// <param name="address"> The transport-specific address value. </param>
        /// <param name="connectionHandler"> The connection handler dependency. </param>
        /// <param name="onStarted"> The callback invoked after transport listener successfully starts accepting connections. </param>
        /// <param name="onConnectionCompleted"> The callback invoked after a connection exchange is completed and its transport stream is closed. </param>
        /// <param name="cancellationToken"> The cancellation token for listener lifecycle. </param>
        Task RunAsync (
            string address,
            IUnityIpcConnectionHandler connectionHandler,
            Action onStarted,
            Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
            CancellationToken cancellationToken);

        /// <summary> Marks the active listener generation released and initiates non-blocking transport cleanup. </summary>
        void Release ();
    }

    /// <summary> Reserves one transport Run generation before its background task can be scheduled. </summary>
    internal interface IUnityIpcTransportRunReservation
    {
        /// <summary> Reserves the Run generation identified by its unique listener cancellation token. </summary>
        /// <param name="cancellationToken"> The cancellation token that will be passed to the corresponding listener Run. </param>
        void ReserveRun (CancellationToken cancellationToken);
    }
}
