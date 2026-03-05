using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements named-pipe transport accept loop for Unity IPC server. </summary>
    internal sealed class NamedPipeUnityIpcTransportListener : IUnityIpcTransportListener
    {
        private readonly object syncRoot = new object();

        private readonly IDaemonLogger daemonLogger;

        private NamedPipeServerStream activeServerStream;

        /// <summary> Initializes a new instance of the <see cref="NamedPipeUnityIpcTransportListener" /> class. </summary>
        /// <param name="daemonLogger"> The daemon daemon-logger dependency. </param>
        public NamedPipeUnityIpcTransportListener (IDaemonLogger daemonLogger = null)
        {
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
        }

        /// <summary> Gets transport kind handled by this listener. </summary>
        public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

        /// <summary> Runs transport-specific accept loop until cancellation is requested. </summary>
        /// <param name="address"> The pipe name value. </param>
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
                throw new ArgumentException("Pipe address must not be empty or whitespace.", nameof(address));
            }

            if (connectionHandler == null)
            {
                throw new ArgumentNullException(nameof(connectionHandler));
            }

            if (onStarted == null)
            {
                throw new ArgumentNullException(nameof(onStarted));
            }

            var started = false;
            while (!cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var serverStream = new NamedPipeServerStream(
                    address,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                lock (syncRoot)
                {
                    activeServerStream = serverStream;
                }

                if (!started)
                {
                    onStarted();
                    started = true;
                }

                try
                {
                    await serverStream.WaitForConnectionAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    await connectionHandler.Handle(serverStream, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (IOException exception) when (cancellationToken.IsCancellationRequested)
                {
                    daemonLogger.Info(
                        DaemonLogCategories.Transport,
                        $"Named pipe listener stopped: {exception.Message}");
                    return;
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested && (exception is IOException or InvalidDataException))
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Transport,
                        $"Named pipe listener ignored recoverable connection error: {exception.Message}");
                }
                finally
                {
                    lock (syncRoot)
                    {
                        if (ReferenceEquals(activeServerStream, serverStream))
                        {
                            activeServerStream = null;
                        }
                    }
                }
            }
        }

        /// <summary> Releases active transport handles to unblock accept loops. </summary>
        public void Release ()
        {
            lock (syncRoot)
            {
                if (activeServerStream != null)
                {
                    activeServerStream.Dispose();
                    activeServerStream = null;
                }
            }
        }
    }
}
