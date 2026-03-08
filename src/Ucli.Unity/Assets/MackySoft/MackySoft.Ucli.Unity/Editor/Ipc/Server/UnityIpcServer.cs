using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Hosts Unity-side IPC server lifecycle and delegates transport loops to transport listeners. </summary>
    internal sealed class UnityIpcServer : IUnityIpcServer
    {
        private static readonly TimeSpan WaitForTerminationRaceGracePeriod = TimeSpan.FromMilliseconds(10);

        private readonly object syncRoot = new object();
        private readonly IUnityIpcRequestProcessor requestProcessor;
        private readonly IUnityIpcConnectionHandler connectionHandler;
        private readonly IReadOnlyList<IUnityIpcTransportListener> transportListeners;
        private readonly IDaemonLogger daemonLogger;

        private CancellationTokenSource? listenerCancellationTokenSource;
        private Task? listenerTask;

        private bool isRunning;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcServer" /> class. </summary>
        /// <param name="requestProcessor"> The shared request-processor dependency. </param>
        /// <param name="connectionHandler"> The connection-handler dependency. </param>
        /// <param name="transportListeners"> The transport-listener dependencies. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public UnityIpcServer (
            IUnityIpcRequestProcessor requestProcessor,
            IUnityIpcConnectionHandler connectionHandler,
            IReadOnlyList<IUnityIpcTransportListener> transportListeners,
            IDaemonLogger daemonLogger = null)
        {
            this.requestProcessor = requestProcessor ?? throw new ArgumentNullException(nameof(requestProcessor));
            this.connectionHandler = connectionHandler ?? throw new ArgumentNullException(nameof(connectionHandler));
            this.transportListeners = transportListeners ?? throw new ArgumentNullException(nameof(transportListeners));
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
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

            UnityIpcServerStartupCoordinator startupCoordinator;
            lock (syncRoot)
            {
                if (isRunning)
                {
                    return;
                }

                isRunning = true;
                listenerCancellationTokenSource = new CancellationTokenSource();
                startupCoordinator = new UnityIpcServerStartupCoordinator();
                listenerTask = Task.Run(() => RunServerLoop(endpoint, startupCoordinator, listenerCancellationTokenSource.Token));
            }

            try
            {
                await startupCoordinator.Wait(cancellationToken);
            }
            catch
            {
                Task? capturedListenerTask;
                CancellationTokenSource? capturedCancellationTokenSource;
                lock (syncRoot)
                {
                    isRunning = false;
                    capturedListenerTask = listenerTask;
                    capturedCancellationTokenSource = listenerCancellationTokenSource;
                    listenerTask = null;
                    listenerCancellationTokenSource = null;
                }

                await CleanupListenerAfterFailedStart(capturedListenerTask, capturedCancellationTokenSource);
                throw;
            }
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

            var transportReleased = false;
            try
            {
                if (capturedCancellationTokenSource != null)
                {
                    capturedCancellationTokenSource.Cancel();
                }

                ReleaseTransportHandles();
                transportReleased = true;

                if (capturedListenerTask != null)
                {
                    await capturedListenerTask;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                daemonLogger.Info(
                    DaemonLogCategories.Ipc,
                    "IPC server stop observed internal listener cancellation.");
            }
            catch (ObjectDisposedException exception)
            {
                daemonLogger.Info(
                    DaemonLogCategories.Ipc,
                    $"IPC server stop observed disposed transport handle. {exception.Message}");
            }
            catch (SocketException exception)
            {
                daemonLogger.Info(
                    DaemonLogCategories.Ipc,
                    $"IPC server stop observed socket shutdown. {exception.SocketErrorCode}");
            }
            finally
            {
                capturedCancellationTokenSource?.Dispose();
                if (!transportReleased)
                {
                    ReleaseTransportHandles();
                }
            }
        }

        /// <summary> Waits until the active listener loop terminates. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes when listener loop terminates, or immediately when server has not been started. </returns>
        /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled before listener loop terminates. </exception>
        public async Task WaitForTermination (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Task? capturedListenerTask;
            lock (syncRoot)
            {
                capturedListenerTask = listenerTask;
            }

            if (capturedListenerTask == null)
            {
                return;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                await capturedListenerTask;
                return;
            }

            var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var completedTask = await Task.WhenAny(capturedListenerTask, cancellationTask);
            if (!ReferenceEquals(completedTask, capturedListenerTask) && !capturedListenerTask.IsCompleted)
            {
                var raceCompletionTask = await Task.WhenAny(capturedListenerTask, Task.Delay(WaitForTerminationRaceGracePeriod));
                if (!ReferenceEquals(raceCompletionTask, capturedListenerTask) && !capturedListenerTask.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            await capturedListenerTask;
        }

        /// <summary> Handles one IPC request through the configured request-handler pipeline. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The IPC response envelope. </returns>
        public Task<IpcResponse> HandleRequest (
            IpcRequest request,
            CancellationToken cancellationToken = default)
        {
            return requestProcessor.Process(request, cancellationToken);
        }

        /// <summary> Runs endpoint-specific listener loop. </summary>
        /// <param name="endpoint"> The configured IPC endpoint. </param>
        /// <param name="cancellationToken"> The cancellation token for listener lifecycle. </param>
        private async Task RunServerLoop (
            IpcEndpoint endpoint,
            UnityIpcServerStartupCoordinator startupCoordinator,
            CancellationToken cancellationToken)
        {
            try
            {
                var listener = ResolveTransportListener(endpoint.TransportKind);
                await listener.Run(
                    endpoint.Address,
                    connectionHandler,
                    startupCoordinator.Complete,
                    cancellationToken);
                startupCoordinator.FailOnUnexpectedExit(cancellationToken.IsCancellationRequested, IsRunning);
            }
            catch (OperationCanceledException) when (!IsRunning || cancellationToken.IsCancellationRequested)
            {
                startupCoordinator.Cancel();
                daemonLogger.Info(
                    DaemonLogCategories.Ipc,
                    "IPC server loop canceled during shutdown.");
            }
            catch (ObjectDisposedException exception) when (!IsRunning || cancellationToken.IsCancellationRequested)
            {
                startupCoordinator.Cancel();
                daemonLogger.Info(
                    DaemonLogCategories.Ipc,
                    $"IPC server loop transport disposed during shutdown. {exception.Message}");
            }
            catch (SocketException exception) when (!IsRunning || cancellationToken.IsCancellationRequested)
            {
                startupCoordinator.Cancel();
                daemonLogger.Info(
                    DaemonLogCategories.Ipc,
                    $"IPC server loop socket closed during shutdown. {exception.SocketErrorCode}");
            }
            catch (Exception exception)
            {
                startupCoordinator.Fail(exception);

                lock (syncRoot)
                {
                    isRunning = false;
                }

                daemonLogger.Exception(
                    DaemonLogCategories.Ipc,
                    "IPC server loop failed unexpectedly.",
                    exception);
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

        /// <summary> Cancels and joins listener resources after start-up failure path. </summary>
        /// <param name="capturedListenerTask"> The captured listener task from start path. </param>
        /// <param name="capturedCancellationTokenSource"> The captured cancellation-token source from start path. </param>
        /// <returns> A task that completes after cleanup steps finish. </returns>
        private async Task CleanupListenerAfterFailedStart (
            Task? capturedListenerTask,
            CancellationTokenSource? capturedCancellationTokenSource)
        {
            var transportReleased = false;
            try
            {
                if (capturedCancellationTokenSource != null)
                {
                    capturedCancellationTokenSource.Cancel();
                }

                ReleaseTransportHandles();
                transportReleased = true;

                if (capturedListenerTask != null)
                {
                    await capturedListenerTask;
                }
            }
            catch (OperationCanceledException)
            {
                daemonLogger.Info(
                    DaemonLogCategories.Ipc,
                    "IPC server start cleanup observed listener cancellation.");
            }
            catch (ObjectDisposedException exception)
            {
                daemonLogger.Info(
                    DaemonLogCategories.Ipc,
                    $"IPC server start cleanup observed disposed transport handle. {exception.Message}");
            }
            catch (SocketException exception)
            {
                daemonLogger.Info(
                    DaemonLogCategories.Ipc,
                    $"IPC server start cleanup observed socket shutdown. {exception.SocketErrorCode}");
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    $"IPC server start cleanup observed listener failure. {exception.Message}");
            }
            finally
            {
                capturedCancellationTokenSource?.Dispose();
                if (!transportReleased)
                {
                    ReleaseTransportHandles();
                }
            }
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
