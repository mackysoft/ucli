using System;
using System.Threading;

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
        /// <param name="cancellationToken"> The cancellation token for listener lifecycle. </param>
        void Run (
            string address,
            IUnityIpcConnectionHandler connectionHandler,
            Action onStarted,
            CancellationToken cancellationToken);

        /// <summary> Releases active transport handles to unblock accept loops. </summary>
        void Release ();
    }
}
