using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements unix-domain-socket transport accept loop for Unity IPC server. </summary>
    internal sealed class UnixDomainSocketUnityIpcTransportListener : IUnityIpcTransportListener
    {
        private readonly object syncRoot = new object();

        private readonly IDaemonLogger daemonLogger;

        private Socket activeListenerSocket;

        private UnityIpcTransportConnectionGroup activeConnectionGroup;

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
        public async Task RunAsync (
            string address,
            IUnityIpcConnectionHandler connectionHandler,
            Action onStarted,
            Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
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

            if (onConnectionCompleted == null)
            {
                throw new ArgumentNullException(nameof(onConnectionCompleted));
            }

            var accessBoundary = new UnixSocketAccessBoundary(address, UcliIpcEndpointNames.DaemonAddressPrefix);
            UnixSocketPathUtilities.ValidateSocketPathLength(address, nameof(address));
            accessBoundary.PrepareForBind();

            using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endPoint = new UnixDomainSocketEndPoint(address);
            listener.Bind(endPoint);
            accessBoundary.HardenBoundSocket();
            listener.Listen(8);

            var connectionGroup = new UnityIpcTransportConnectionGroup(daemonLogger);
            lock (syncRoot)
            {
                activeListenerSocket = listener;
                activeConnectionGroup = connectionGroup;
            }

            onStarted();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var acceptedConnection = new AcceptedUnixDomainSocketConnection(await listener.AcceptAsync());
                        connectionGroup.Start(
                            acceptedConnection,
                            () => connectionHandler.HandleAsync(acceptedConnection.Stream, cancellationToken),
                            onConnectionCompleted,
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception) when (cancellationToken.IsCancellationRequested && exception is ObjectDisposedException or SocketException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return;
                    }
                    catch (Exception exception) when (!cancellationToken.IsCancellationRequested && (exception is IOException or InvalidDataException or SocketException))
                    {
                        // NOTE: Probe callers may close timed-out sockets while Unity is busy on the main thread.
                        // Emitting these expected connection-local failures to the Unity console can make recovery slower.
                    }
                }
            }
            finally
            {
                connectionGroup.Release();
                await connectionGroup.WaitForCompletionAsync();

                lock (syncRoot)
                {
                    if (ReferenceEquals(activeListenerSocket, listener))
                    {
                        activeListenerSocket = null;
                    }

                    if (ReferenceEquals(activeConnectionGroup, connectionGroup))
                    {
                        activeConnectionGroup = null;
                    }
                }

                accessBoundary.Cleanup();
            }
        }

        /// <summary> Releases active transport handles to unblock accept loops. </summary>
        public void Release ()
        {
            UnityIpcTransportConnectionGroup connectionGroup;
            lock (syncRoot)
            {
                if (activeListenerSocket != null)
                {
                    activeListenerSocket.Dispose();
                    activeListenerSocket = null;
                }

                connectionGroup = activeConnectionGroup;
            }

            connectionGroup?.Release();
        }

        private sealed class AcceptedUnixDomainSocketConnection : IDisposable
        {
            private readonly Socket socket;

            private readonly NetworkStream stream;

            private int disposed;

            public AcceptedUnixDomainSocketConnection (Socket socket)
            {
                this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
                stream = new NetworkStream(socket, ownsSocket: false);
            }

            public Stream Stream => stream;

            public void Dispose ()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                {
                    return;
                }

                try
                {
                    stream.Dispose();
                }
                catch (Exception exception) when (exception is ObjectDisposedException or IOException or SocketException or InvalidOperationException)
                {
                }

                try
                {
                    socket.Dispose();
                }
                catch (Exception exception) when (exception is ObjectDisposedException or SocketException or InvalidOperationException)
                {
                }
            }
        }
    }
}
