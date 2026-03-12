using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements unix-domain-socket transport accept loop for Unity IPC server. </summary>
    internal sealed class UnixDomainSocketUnityIpcTransportListener : IUnityIpcTransportListener
    {
        private readonly object syncRoot = new object();

        private readonly IDaemonLogger daemonLogger;

        private Socket activeListenerSocket;

        /// <summary> Initializes a new instance of the <see cref="UnixDomainSocketUnityIpcTransportListener" /> class. </summary>
        /// <param name="daemonLogger"> The daemon daemon-logger dependency. </param>
        public UnixDomainSocketUnityIpcTransportListener (IDaemonLogger daemonLogger = null)
        {
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
        }

        /// <summary> Gets transport kind handled by this listener. </summary>
        public IpcTransportKind TransportKind => IpcTransportKind.UnixDomainSocket;

        /// <summary> Runs transport-specific accept loop until cancellation is requested. </summary>
        /// <param name="address"> The unix-domain-socket path value. </param>
        /// <param name="connectionHandler"> The connection handler dependency. </param>
        /// <param name="cancellationToken"> The cancellation token for listener lifecycle. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="address" /> is empty. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="connectionHandler" /> is <see langword="null" />. </exception>
        public async Task Run (
            string address,
            IUnityIpcConnectionHandler connectionHandler,
            Action onStarted,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Socket address must not be empty or whitespace.", nameof(address));
            }

            if (connectionHandler == null)
            {
                throw new ArgumentNullException(nameof(connectionHandler));
            }

            if (onStarted == null)
            {
                throw new ArgumentNullException(nameof(onStarted));
            }

            var socketDirectoryPath = Path.GetDirectoryName(address);
            if (!string.IsNullOrWhiteSpace(socketDirectoryPath))
            {
                FileUtilities.EnsureStorageDirectoryExists(socketDirectoryPath);
            }

            if (File.Exists(address))
            {
                File.Delete(address);
            }

            using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endPoint = new UnixDomainSocketEndPoint(address);
            listener.Bind(endPoint);
            listener.Listen(8);

            lock (syncRoot)
            {
                activeListenerSocket = listener;
            }

            onStarted();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using var acceptedSocket = await listener.AcceptAsync();
                        using var networkStream = new NetworkStream(acceptedSocket, ownsSocket: false);
                        await connectionHandler.Handle(networkStream, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception) when (!cancellationToken.IsCancellationRequested && (exception is IOException or InvalidDataException or SocketException))
                    {
                        daemonLogger.Warning(
                            DaemonLogCategories.Transport,
                            $"Unix domain socket listener ignored recoverable connection error: {exception.Message}");
                    }
                }
            }
            finally
            {
                lock (syncRoot)
                {
                    if (ReferenceEquals(activeListenerSocket, listener))
                    {
                        activeListenerSocket = null;
                    }
                }

                if (File.Exists(address))
                {
                    File.Delete(address);
                }
            }
        }

        /// <summary> Releases active transport handles to unblock accept loops. </summary>
        public void Release ()
        {
            lock (syncRoot)
            {
                if (activeListenerSocket != null)
                {
                    activeListenerSocket.Dispose();
                    activeListenerSocket = null;
                }
            }
        }
    }
}
