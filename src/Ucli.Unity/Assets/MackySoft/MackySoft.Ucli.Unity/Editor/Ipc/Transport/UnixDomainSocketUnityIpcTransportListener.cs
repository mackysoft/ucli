using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements unix-domain-socket transport accept loop for Unity IPC server. </summary>
    internal sealed class UnixDomainSocketUnityIpcTransportListener : IUnityIpcTransportListener
    {
        private readonly object syncRoot = new object();

        private Socket activeListenerSocket;

        /// <summary> Gets transport kind handled by this listener. </summary>
        public IpcTransportKind TransportKind => IpcTransportKind.UnixDomainSocket;

        /// <summary> Runs transport-specific accept loop until cancellation is requested. </summary>
        /// <param name="address"> The unix-domain-socket path value. </param>
        /// <param name="connectionHandler"> The connection handler dependency. </param>
        /// <param name="cancellationToken"> The cancellation token for listener lifecycle. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="address" /> is empty. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="connectionHandler" /> is <see langword="null" />. </exception>
        public void Run (
            string address,
            IUnityIpcConnectionHandler connectionHandler,
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

            var socketDirectoryPath = Path.GetDirectoryName(address);
            if (!string.IsNullOrWhiteSpace(socketDirectoryPath))
            {
                Directory.CreateDirectory(socketDirectoryPath);
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

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using var acceptedSocket = listener.Accept();
                        using var networkStream = new NetworkStream(acceptedSocket, ownsSocket: false);
                        connectionHandler.Handle(networkStream, cancellationToken).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception) when (!cancellationToken.IsCancellationRequested && (exception is IOException or InvalidDataException or SocketException))
                    {
                        Debug.LogWarning($"Unix domain socket listener ignored recoverable connection error: {exception.Message}");
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
