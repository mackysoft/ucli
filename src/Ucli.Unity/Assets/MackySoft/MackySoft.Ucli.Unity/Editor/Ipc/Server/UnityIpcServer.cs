using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Hosts Unity-side IPC server lifecycle and delegates transport loops to transport listeners. </summary>
    internal sealed class UnityIpcServer : IUnityIpcServer
    {
        private static readonly TimeSpan WaitForTerminationRaceGracePeriod = TimeSpan.FromMilliseconds(10);

        private readonly object syncRoot = new object();
        private readonly IUnityIpcConnectionHandler connectionHandler;
        private readonly IReadOnlyList<IUnityIpcTransportListener> transportListeners;
        private readonly IDaemonShutdownSignal daemonShutdownSignal;
        private readonly IDaemonLogger daemonLogger;

        private CancellationTokenSource? listenerCancellationTokenSource;
        private Task? listenerTask;

        private bool isRunning;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcServer" /> class. </summary>
        /// <param name="connectionHandler"> The connection-handler dependency. </param>
        /// <param name="transportListeners"> The transport-listener dependencies. </param>
        /// <param name="daemonShutdownSignal"> The daemon shutdown signal dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public UnityIpcServer (
            IUnityIpcConnectionHandler connectionHandler,
            IReadOnlyList<IUnityIpcTransportListener> transportListeners,
            IDaemonShutdownSignal daemonShutdownSignal,
            IDaemonLogger daemonLogger = null)
        {
            this.connectionHandler = connectionHandler ?? throw new ArgumentNullException(nameof(connectionHandler));
            this.transportListeners = transportListeners ?? throw new ArgumentNullException(nameof(transportListeners));
            this.daemonShutdownSignal = daemonShutdownSignal ?? throw new ArgumentNullException(nameof(daemonShutdownSignal));
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
        public async Task StartAsync (
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
                listenerTask = Task.Run(() => RunServerLoopAsync(endpoint, startupCoordinator, listenerCancellationTokenSource.Token));
            }

            try
            {
                await startupCoordinator.WaitAsync(cancellationToken);
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

                await CleanupListenerAfterFailedStartAsync(capturedListenerTask, capturedCancellationTokenSource);
                throw;
            }
        }

        /// <summary> Stops the IPC server lifecycle and releases endpoint resources. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes when background listener loop terminates. </returns>
        public async Task StopAsync (CancellationToken cancellationToken = default)
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
                    await capturedListenerTask.ConfigureAwait(false);
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
        public async Task WaitForTerminationAsync (CancellationToken cancellationToken = default)
        {
            Task? capturedListenerTask;
            lock (syncRoot)
            {
                capturedListenerTask = listenerTask;
            }

            if (capturedListenerTask == null)
            {
                return;
            }

            await CancellationGracePeriodAwaiter.WaitAsync(capturedListenerTask, cancellationToken, WaitForTerminationRaceGracePeriod)
                .ConfigureAwait(false);
        }

        /// <summary> Runs endpoint-specific listener loop. </summary>
        /// <param name="endpoint"> The configured IPC endpoint. </param>
        /// <param name="cancellationToken"> The cancellation token for listener lifecycle. </param>
        private async Task RunServerLoopAsync (
            IpcEndpoint endpoint,
            UnityIpcServerStartupCoordinator startupCoordinator,
            CancellationToken cancellationToken)
        {
            try
            {
                var listener = ResolveTransportListener(endpoint.TransportKind);
                await listener.RunAsync(
                    endpoint.Address,
                    connectionHandler,
                    startupCoordinator.Complete,
                    HandleConnectionCompleted,
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

        private void HandleConnectionCompleted (UnityIpcConnectionHandleResult result)
        {
            if (ShouldSignalDaemonShutdown(result))
            {
                daemonShutdownSignal.Signal();
            }
        }

        private static bool ShouldSignalDaemonShutdown (UnityIpcConnectionHandleResult result)
        {
            return result.Request != null
                && result.Response != null
                && string.Equals(result.Request.Method, IpcMethodNames.Shutdown, StringComparison.Ordinal)
                && string.Equals(result.Response.Status, IpcProtocol.StatusOk, StringComparison.Ordinal)
                && result.Response.Errors.Count == 0;
        }

        /// <summary> Cancels and joins listener resources after start-up failure path. </summary>
        /// <param name="capturedListenerTask"> The captured listener task from start path. </param>
        /// <param name="capturedCancellationTokenSource"> The captured cancellation-token source from start path. </param>
        /// <returns> A task that completes after cleanup steps finish. </returns>
        private async Task CleanupListenerAfterFailedStartAsync (
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
                    await capturedListenerTask.ConfigureAwait(false);
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
