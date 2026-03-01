using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Hosts Unity-side IPC server lifecycle and delegates transport loops to transport listeners. </summary>
    internal sealed class UnityIpcServer : IUnityIpcServer
    {
        private readonly object syncRoot = new object();
        private readonly IUnityIpcRequestHandler requestHandler;
        private readonly IUnityIpcConnectionHandler connectionHandler;
        private readonly IReadOnlyList<IUnityIpcTransportListener> transportListeners;

        private CancellationTokenSource? listenerCancellationTokenSource;
        private Task? listenerTask;

        private bool isRunning;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcServer" /> class. </summary>
        /// <param name="requestHandler"> The request-handler dependency. </param>
        /// <param name="connectionHandler"> The connection-handler dependency. </param>
        /// <param name="transportListeners"> The transport-listener dependencies. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public UnityIpcServer (
            IUnityIpcRequestHandler requestHandler,
            IUnityIpcConnectionHandler connectionHandler,
            IReadOnlyList<IUnityIpcTransportListener> transportListeners)
        {
            this.requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
            this.connectionHandler = connectionHandler ?? throw new ArgumentNullException(nameof(connectionHandler));
            this.transportListeners = transportListeners ?? throw new ArgumentNullException(nameof(transportListeners));
        }

        /// <summary> Gets a value indicating whether the server lifecycle is marked as started. </summary>
        public bool IsRunning
        {
            get
            {
                lock (syncRoot)
                {
                    return isRunning;
                }
            }
        }

        /// <summary> Starts the IPC server listener for the specified endpoint. </summary>
        /// <param name="endpoint"> The endpoint definition used by server binding. Must not be <see langword="null" />, and its address must not be empty or whitespace. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes after listener task has been scheduled. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="endpoint" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when endpoint address is empty or whitespace. </exception>
        public async Task Start (
            IpcEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (string.IsNullOrWhiteSpace(endpoint.Address))
            {
                throw new ArgumentException("Endpoint address must not be empty or whitespace.", nameof(endpoint));
            }

            lock (syncRoot)
            {
                if (isRunning)
                {
                    return;
                }

                isRunning = true;
                listenerCancellationTokenSource = new CancellationTokenSource();
                listenerTask = Task.Run(() => RunServerLoop(endpoint, listenerCancellationTokenSource.Token));
            }

            await ObserveStartupFailure(cancellationToken);
        }

        /// <summary> Stops the IPC server lifecycle and releases endpoint resources. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes when background listener loop terminates. </returns>
        public async Task Stop (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Task? capturedListenerTask;
            CancellationTokenSource? capturedCancellationTokenSource;
            lock (syncRoot)
            {
                if (!isRunning && listenerTask == null)
                {
                    return;
                }

                isRunning = false;
                capturedListenerTask = listenerTask;
                capturedCancellationTokenSource = listenerCancellationTokenSource;
                listenerTask = null;
                listenerCancellationTokenSource = null;
            }

            try
            {
                if (capturedCancellationTokenSource != null)
                {
                    capturedCancellationTokenSource.Cancel();
                }

                ReleaseTransportHandles();

                if (capturedListenerTask != null)
                {
                    await capturedListenerTask;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Debug.Log("IPC server stop observed internal listener cancellation.");
            }
            catch (ObjectDisposedException exception)
            {
                Debug.Log($"IPC server stop observed disposed transport handle. {exception.Message}");
            }
            catch (SocketException exception)
            {
                Debug.Log($"IPC server stop observed socket shutdown. {exception.SocketErrorCode}");
            }
            finally
            {
                capturedCancellationTokenSource?.Dispose();
                ReleaseTransportHandles();
            }
        }

        /// <summary> Handles one IPC request through the configured request-handler pipeline. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The IPC response envelope. </returns>
        public Task<IpcResponse> HandleRequest (
            IpcRequest request,
            CancellationToken cancellationToken = default)
        {
            return requestHandler.Handle(request, cancellationToken);
        }

        /// <summary> Runs endpoint-specific listener loop. </summary>
        /// <param name="endpoint"> The configured IPC endpoint. </param>
        /// <param name="cancellationToken"> The cancellation token for listener lifecycle. </param>
        private void RunServerLoop (
            IpcEndpoint endpoint,
            CancellationToken cancellationToken)
        {
            try
            {
                var listener = ResolveTransportListener(endpoint.TransportKind);
                listener.Run(endpoint.Address, connectionHandler, cancellationToken);
            }
            catch (OperationCanceledException) when (!IsRunning || cancellationToken.IsCancellationRequested)
            {
                Debug.Log("IPC server loop canceled during shutdown.");
            }
            catch (ObjectDisposedException exception) when (!IsRunning || cancellationToken.IsCancellationRequested)
            {
                Debug.Log($"IPC server loop transport disposed during shutdown. {exception.Message}");
            }
            catch (SocketException exception) when (!IsRunning || cancellationToken.IsCancellationRequested)
            {
                Debug.Log($"IPC server loop socket closed during shutdown. {exception.SocketErrorCode}");
            }
            catch (Exception exception)
            {
                lock (syncRoot)
                {
                    isRunning = false;
                }

                Debug.LogException(exception);
                throw;
            }
        }

        /// <summary> Observes immediate listener failures during startup scheduling. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes when immediate startup failure check is done. </returns>
        private async Task ObserveStartupFailure (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Yield();

            Task? capturedListenerTask;
            lock (syncRoot)
            {
                capturedListenerTask = listenerTask;
            }

            if (capturedListenerTask == null || !capturedListenerTask.IsCompleted)
            {
                return;
            }

            try
            {
                await capturedListenerTask;
            }
            catch
            {
                lock (syncRoot)
                {
                    isRunning = false;
                    listenerTask = null;
                    listenerCancellationTokenSource?.Dispose();
                    listenerCancellationTokenSource = null;
                }

                throw;
            }
        }

        /// <summary> Resolves one transport listener for the specified transport kind. </summary>
        /// <param name="transportKind"> The transport kind to resolve. </param>
        /// <returns> The transport listener instance. </returns>
        /// <exception cref="InvalidOperationException"> Thrown when transport listener is not registered for the kind. </exception>
        private IUnityIpcTransportListener ResolveTransportListener (IpcTransportKind transportKind)
        {
            foreach (var listener in transportListeners)
            {
                if (listener.TransportKind == transportKind)
                {
                    return listener;
                }
            }

            throw new InvalidOperationException($"Unsupported transport kind '{transportKind}'.");
        }

        /// <summary> Releases transport handles used by active listener loops. </summary>
        private void ReleaseTransportHandles ()
        {
            foreach (var listener in transportListeners)
            {
                listener.Release();
            }
        }
    }
}
