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
        internal static readonly TimeSpan DefaultListenerStopTimeout = TimeSpan.FromSeconds(2);

        private static readonly TimeSpan WaitForTerminationRaceGracePeriod = TimeSpan.FromMilliseconds(10);

        private readonly object syncRoot = new object();
        private readonly IUnityIpcConnectionHandler connectionHandler;
        private readonly IReadOnlyList<IUnityIpcTransportListener> transportListeners;
        private readonly IDaemonShutdownSignal daemonShutdownSignal;
        private readonly IDaemonLogger daemonLogger;
        private readonly TimeSpan listenerStopTimeout;

        private CancellationTokenSource? listenerCancellationTokenSource;
        private Task? listenerTask;

        private TaskCompletionSource<bool>? lifecycleTransitionCompletionSource;

        private TaskCompletionSource<bool>? lifecycleTransportReleaseCompletionSource;

        private int lifecycleTransportReleaseCount;

        private ListenerGenerationPublicationState activeListenerPublicationState;

        private bool isRunning;

        private bool isRestartBlocked;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcServer" /> class. </summary>
        /// <param name="connectionHandler"> The connection-handler dependency. </param>
        /// <param name="transportListeners"> The transport-listener dependencies. </param>
        /// <param name="daemonShutdownSignal"> The daemon shutdown signal dependency. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        /// <param name="listenerStopTimeout"> The maximum time allowed for the listener generation to terminate after transport release. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="listenerStopTimeout" /> is not positive. </exception>
        public UnityIpcServer (
            IUnityIpcConnectionHandler connectionHandler,
            IReadOnlyList<IUnityIpcTransportListener> transportListeners,
            IDaemonShutdownSignal daemonShutdownSignal,
            IDaemonLogger daemonLogger,
            TimeSpan listenerStopTimeout)
        {
            this.connectionHandler = connectionHandler ?? throw new ArgumentNullException(nameof(connectionHandler));
            this.transportListeners = transportListeners ?? throw new ArgumentNullException(nameof(transportListeners));
            this.daemonShutdownSignal = daemonShutdownSignal ?? throw new ArgumentNullException(nameof(daemonShutdownSignal));
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
            if (listenerStopTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(listenerStopTimeout),
                    listenerStopTimeout,
                    "Listener stop timeout must be greater than zero.");
            }

            this.listenerStopTimeout = listenerStopTimeout;
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
        /// <returns> A task that produces the listener generation fence required before durable endpoint publication. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="endpoint" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when endpoint address is empty or whitespace. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when a listener is already active or a prior listener generation did not terminate safely. </exception>
        public async Task<IUnityIpcServerPublicationFence> StartAsync (
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

            ListenerGenerationPublicationState nextListenerPublicationState;
            CancellationTokenSource nextListenerCancellationTokenSource;
            IUnityIpcTransportListener nextTransportListener;
            Task nextListenerTask;
            UnityIpcServerStartupCoordinator startupCoordinator;
            while (true)
            {
                Task? lifecycleTransitionTask;
                lock (syncRoot)
                {
                    if (isRestartBlocked)
                    {
                        throw new InvalidOperationException(
                            "The previous IPC listener generation did not terminate within its stop deadline. Restart the Unity Editor before starting another listener generation.");
                    }

                    if (isRunning)
                    {
                        throw new InvalidOperationException(
                            "The IPC server already has an active listener generation.");
                    }

                    lifecycleTransitionTask = lifecycleTransitionCompletionSource?.Task
                        ?? lifecycleTransportReleaseCompletionSource?.Task;
                    if (lifecycleTransitionTask == null)
                    {
                        nextTransportListener = ResolveTransportListener(endpoint.TransportKind);
                        nextListenerPublicationState = new ListenerGenerationPublicationState();
                        nextListenerCancellationTokenSource = new CancellationTokenSource();
                        startupCoordinator = new UnityIpcServerStartupCoordinator();
                        try
                        {
                            if (nextTransportListener is IUnityIpcTransportRunReservation runReservation)
                            {
                                runReservation.ReserveRun(nextListenerCancellationTokenSource.Token);
                            }
                        }
                        catch
                        {
                            nextListenerCancellationTokenSource.Dispose();
                            throw;
                        }

                        isRunning = true;
                        activeListenerPublicationState = nextListenerPublicationState;
                        listenerCancellationTokenSource = nextListenerCancellationTokenSource;
                        nextListenerTask = Task.Run(() => RunServerLoopAsync(
                            endpoint,
                            nextTransportListener,
                            startupCoordinator,
                            nextListenerPublicationState,
                            nextListenerCancellationTokenSource));
                        listenerTask = nextListenerTask;
                        break;
                    }
                }

                await CancellationGracePeriodAwaiter.WaitAsync(
                        lifecycleTransitionTask,
                        cancellationToken,
                        TimeSpan.Zero)
                    .ConfigureAwait(false);
            }

            try
            {
                await startupCoordinator.WaitAsync(cancellationToken);
                if (!IsListenerGenerationAvailableForPublication(nextListenerPublicationState))
                {
                    throw new OperationCanceledException(
                        "IPC listener generation was released before startup completed.");
                }

                return new ListenerGenerationPublicationFence(this, nextListenerPublicationState);
            }
            catch
            {
                var ownsActiveGeneration = false;
                TaskCompletionSource<bool>? lifecycleTransition = null;
                lock (syncRoot)
                {
                    if (ReferenceEquals(activeListenerPublicationState, nextListenerPublicationState))
                    {
                        ownsActiveGeneration = true;
                        lifecycleTransition = BeginLifecycleTransition();
                        isRunning = false;
                        activeListenerPublicationState = null;
                        if (ReferenceEquals(listenerTask, nextListenerTask))
                        {
                            listenerTask = null;
                        }

                        if (ReferenceEquals(listenerCancellationTokenSource, nextListenerCancellationTokenSource))
                        {
                            listenerCancellationTokenSource = null;
                        }
                    }
                }

                try
                {
                    await CleanupListenerAfterFailedStartAsync(
                        nextListenerTask,
                        nextListenerCancellationTokenSource,
                        releaseTransportHandles: ownsActiveGeneration);
                }
                finally
                {
                    CompleteLifecycleTransition(lifecycleTransition);
                }

                throw;
            }
        }

        /// <summary> Stops the IPC server lifecycle and releases endpoint resources. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes when background listener loop terminates. </returns>
        /// <exception cref="TimeoutException"> Thrown when the listener generation does not terminate before the configured stop deadline. </exception>
        public async Task StopAsync (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Task? capturedListenerTask;
            CancellationTokenSource? capturedCancellationTokenSource;
            TaskCompletionSource<bool> lifecycleTransition;
            while (true)
            {
                Task? pendingLifecycleTransitionTask;
                lock (syncRoot)
                {
                    pendingLifecycleTransitionTask = lifecycleTransitionCompletionSource?.Task;
                    if (pendingLifecycleTransitionTask == null)
                    {
                        if (!isRunning && listenerTask == null)
                        {
                            return;
                        }

                        lifecycleTransition = BeginLifecycleTransition();
                        isRunning = false;
                        activeListenerPublicationState = null;
                        capturedListenerTask = listenerTask;
                        capturedCancellationTokenSource = listenerCancellationTokenSource;
                        listenerTask = null;
                        listenerCancellationTokenSource = null;
                        break;
                    }
                }

                await CancellationGracePeriodAwaiter.WaitAsync(
                        pendingLifecycleTransitionTask,
                        cancellationToken,
                        TimeSpan.Zero)
                    .ConfigureAwait(false);
            }

            var transportReleased = false;
            var cancellationTask = RequestCancellationAsync(capturedCancellationTokenSource);
            var terminationTask = capturedListenerTask == null
                ? cancellationTask
                : Task.WhenAll(cancellationTask, capturedListenerTask);
            try
            {
                transportReleased = TryReleaseTransportHandles("IPC server stop");

                await WaitForListenerStopAsync(
                        terminationTask,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                lock (syncRoot)
                {
                    isRestartBlocked = true;
                }

                RetainIncompleteTermination(
                    terminationTask,
                    capturedCancellationTokenSource);

                daemonLogger.Error(
                    DaemonLogCategories.Ipc,
                    "IPC server listener did not terminate before the stop deadline. A successor listener will not be started in this Editor process.",
                    exception.ToString());
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                lock (syncRoot)
                {
                    if (!terminationTask.IsCompleted)
                    {
                        isRestartBlocked = true;
                    }
                }

                if (!terminationTask.IsCompleted)
                {
                    RetainIncompleteTermination(
                        terminationTask,
                        capturedCancellationTokenSource);
                }

                throw;
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
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    $"IPC server stop observed listener failure. {exception.Message}");
            }
            finally
            {
                try
                {
                    DisposeCancellationTokenSourceAfterCompletion(
                        capturedCancellationTokenSource,
                        terminationTask);
                    if (!transportReleased)
                    {
                        _ = TryReleaseTransportHandles("IPC server stop retry");
                    }
                }
                finally
                {
                    CompleteLifecycleTransition(lifecycleTransition);
                }
            }
        }

        /// <inheritdoc />
        public void ReleaseForEditorLifecycleEvent ()
        {
            Task? capturedListenerTask = null;
            CancellationTokenSource? capturedCancellationTokenSource = null;
            TaskCompletionSource<bool>? lifecycleTransition = null;
            TaskCompletionSource<bool>? lifecycleTransportRelease = null;
            lock (syncRoot)
            {
                if (lifecycleTransitionCompletionSource != null)
                {
                    lifecycleTransportRelease = BeginLifecycleTransportRelease();
                }
                else if (!isRunning && listenerTask == null && listenerCancellationTokenSource == null)
                {
                    return;
                }
                else
                {
                    lifecycleTransition = BeginLifecycleTransition();
                    isRunning = false;
                    activeListenerPublicationState = null;
                    capturedListenerTask = listenerTask;
                    listenerTask = null;
                    capturedCancellationTokenSource = listenerCancellationTokenSource;
                    listenerCancellationTokenSource = null;
                }
            }

            if (lifecycleTransportRelease != null)
            {
                try
                {
                    _ = TryReleaseTransportHandles("IPC server editor lifecycle release during transition");
                }
                finally
                {
                    CompleteLifecycleTransportRelease(lifecycleTransportRelease);
                }

                return;
            }

            try
            {
                var cancellationTask = RequestCancellationAsync(capturedCancellationTokenSource);
                _ = TryReleaseTransportHandles("IPC server editor lifecycle release");
                var terminationTask = capturedListenerTask == null
                    ? cancellationTask
                    : Task.WhenAll(cancellationTask, capturedListenerTask);
                DisposeCancellationTokenSourceAfterCompletion(
                    capturedCancellationTokenSource,
                    terminationTask);
            }
            finally
            {
                CompleteLifecycleTransition(lifecycleTransition);
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
        /// <param name="listener"> The transport listener reserved for this server generation. </param>
        /// <param name="cancellationToken"> The cancellation token for listener lifecycle. </param>
        private async Task RunServerLoopAsync (
            IpcEndpoint endpoint,
            IUnityIpcTransportListener listener,
            UnityIpcServerStartupCoordinator startupCoordinator,
            ListenerGenerationPublicationState publicationState,
            CancellationTokenSource listenerCancellationTokenSource)
        {
            var cancellationToken = listenerCancellationTokenSource.Token;
            TaskCompletionSource<bool>? listenerFaultTransition = null;
            try
            {
                try
                {
                    var listenerRunTask = listener.RunAsync(
                        endpoint.Address,
                        connectionHandler,
                        () => SignalStartupForGeneration(publicationState, startupCoordinator),
                        result => HandleConnectionCompleted(publicationState, result),
                        cancellationToken);
                    publicationState.AttachListenerTask(listenerRunTask);
                    if (publicationState.IsListenerRunning)
                    {
                        startupCoordinator.MarkListenerLifetimeTracked();
                    }

                    await listenerRunTask;
                }
                finally
                {
                    // Publication checks observe listenerRunTask directly; this phase preserves
                    // termination after the server loop resumes and before it takes the lifecycle lock.
                    publicationState.MarkTerminationStarted();
                }

                if (!cancellationToken.IsCancellationRequested
                    && IsListenerGenerationRunning(publicationState))
                {
                    throw new InvalidOperationException(
                        "IPC server listener exited unexpectedly while its generation was still active.");
                }

                startupCoordinator.Cancel();
            }
            catch (OperationCanceledException) when (!IsListenerGenerationRunning(publicationState) || cancellationToken.IsCancellationRequested)
            {
                startupCoordinator.Cancel();
                daemonLogger.Info(
                    DaemonLogCategories.Ipc,
                    "IPC server loop canceled during shutdown.");
            }
            catch (ObjectDisposedException exception) when (!IsListenerGenerationRunning(publicationState) || cancellationToken.IsCancellationRequested)
            {
                startupCoordinator.Cancel();
                daemonLogger.Info(
                    DaemonLogCategories.Ipc,
                    $"IPC server loop transport disposed during shutdown. {exception.Message}");
            }
            catch (SocketException exception) when (!IsListenerGenerationRunning(publicationState) || cancellationToken.IsCancellationRequested)
            {
                startupCoordinator.Cancel();
                daemonLogger.Info(
                    DaemonLogCategories.Ipc,
                    $"IPC server loop socket closed during shutdown. {exception.SocketErrorCode}");
            }
            catch (TimeoutException exception) when (!IsListenerGenerationRunning(publicationState) || cancellationToken.IsCancellationRequested)
            {
                startupCoordinator.Cancel();
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    $"IPC server loop exceeded its connection drain deadline during shutdown. {exception.Message}");
                throw;
            }
            catch (Exception exception)
            {
                startupCoordinator.Fail(exception);

                lock (syncRoot)
                {
                    if (ReferenceEquals(activeListenerPublicationState, publicationState))
                    {
                        listenerFaultTransition = BeginLifecycleTransition();
                        isRunning = false;
                        activeListenerPublicationState = null;
                    }
                }

                daemonLogger.Exception(
                    DaemonLogCategories.Ipc,
                    "IPC server loop failed unexpectedly.",
                    exception);
                throw;
            }
            finally
            {
                if (listenerFaultTransition != null)
                {
                    try
                    {
                        lock (syncRoot)
                        {
                            if (ReferenceEquals(this.listenerCancellationTokenSource, listenerCancellationTokenSource))
                            {
                                this.listenerCancellationTokenSource = null;
                            }
                        }

                        listenerCancellationTokenSource.Dispose();
                    }
                    finally
                    {
                        CompleteLifecycleTransition(listenerFaultTransition);
                    }
                }
            }
        }

        private bool IsListenerGenerationRunning (ListenerGenerationPublicationState publicationState)
        {
            lock (syncRoot)
            {
                return isRunning && ReferenceEquals(activeListenerPublicationState, publicationState);
            }
        }

        private bool IsListenerGenerationAvailableForPublication (
            ListenerGenerationPublicationState publicationState)
        {
            lock (syncRoot)
            {
                return IsListenerGenerationAvailableForPublicationWithoutLock(publicationState);
            }
        }

        private void ThrowIfListenerGenerationTerminated (
            ListenerGenerationPublicationState publicationState)
        {
            lock (syncRoot)
            {
                if (!IsListenerGenerationAvailableForPublicationWithoutLock(publicationState))
                {
                    throw new InvalidOperationException(
                        "The IPC listener generation terminated before durable endpoint publication completed.");
                }
            }
        }

        private bool TryCommitListenerGenerationPublication (
            ListenerGenerationPublicationState publicationState,
            Action commitActiveOwnership)
        {
            lock (syncRoot)
            {
                if (!IsListenerGenerationAvailableForPublicationWithoutLock(publicationState)
                    || !publicationState.TryBeginPublicationCommit())
                {
                    return false;
                }

                commitActiveOwnership();
                return true;
            }
        }

        private bool IsListenerGenerationAvailableForPublicationWithoutLock (
            ListenerGenerationPublicationState publicationState)
        {
            return publicationState != null
                && publicationState.IsPublicationPending
                && publicationState.IsListenerRunning
                && ReferenceEquals(activeListenerPublicationState, publicationState)
                && isRunning;
        }

        private void SignalStartupForGeneration (
            ListenerGenerationPublicationState publicationState,
            UnityIpcServerStartupCoordinator startupCoordinator)
        {
            lock (syncRoot)
            {
                if (isRunning && ReferenceEquals(activeListenerPublicationState, publicationState))
                {
                    startupCoordinator.SignalListenerStarted();
                    return;
                }

                startupCoordinator.Cancel();
            }
        }

        private TaskCompletionSource<bool> BeginLifecycleTransition ()
        {
            if (lifecycleTransitionCompletionSource != null)
            {
                throw new InvalidOperationException("An IPC server lifecycle transition is already active.");
            }

            var transition = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lifecycleTransitionCompletionSource = transition;
            return transition;
        }

        private void CompleteLifecycleTransition (TaskCompletionSource<bool>? lifecycleTransition)
        {
            if (lifecycleTransition == null)
            {
                return;
            }

            lock (syncRoot)
            {
                if (ReferenceEquals(lifecycleTransitionCompletionSource, lifecycleTransition))
                {
                    lifecycleTransitionCompletionSource = null;
                }
            }

            lifecycleTransition.TrySetResult(true);
        }

        private TaskCompletionSource<bool> BeginLifecycleTransportRelease ()
        {
            lifecycleTransportReleaseCompletionSource ??=
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lifecycleTransportReleaseCount++;
            return lifecycleTransportReleaseCompletionSource;
        }

        private void CompleteLifecycleTransportRelease (TaskCompletionSource<bool> lifecycleTransportRelease)
        {
            var releaseCompleted = false;
            lock (syncRoot)
            {
                if (!ReferenceEquals(lifecycleTransportReleaseCompletionSource, lifecycleTransportRelease))
                {
                    return;
                }

                lifecycleTransportReleaseCount--;
                if (lifecycleTransportReleaseCount == 0)
                {
                    lifecycleTransportReleaseCompletionSource = null;
                    releaseCompleted = true;
                }
            }

            if (releaseCompleted)
            {
                lifecycleTransportRelease.TrySetResult(true);
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

        private void HandleConnectionCompleted (
            ListenerGenerationPublicationState publicationState,
            UnityIpcConnectionHandleResult result)
        {
            lock (syncRoot)
            {
                if (!isRunning
                    || !ReferenceEquals(activeListenerPublicationState, publicationState)
                    || !ShouldSignalDaemonShutdown(result))
                {
                    return;
                }

                daemonShutdownSignal.Signal();
            }
        }

        private static bool ShouldSignalDaemonShutdown (UnityIpcConnectionHandleResult result)
        {
            return result.IsShutdownAdmissionCommitted
                && UnityIpcShutdownResponsePolicy.IsAccepted(result.Request, result.Response);
        }

        /// <summary> Cancels and joins listener resources after start-up failure path. </summary>
        /// <param name="capturedListenerTask"> The captured listener task from start path. </param>
        /// <param name="capturedCancellationTokenSource"> The captured cancellation-token source from start path. </param>
        /// <param name="releaseTransportHandles"> Whether the failed generation still owns the active transport handles. </param>
        /// <returns> A task that completes after cleanup steps finish. </returns>
        private async Task CleanupListenerAfterFailedStartAsync (
            Task? capturedListenerTask,
            CancellationTokenSource? capturedCancellationTokenSource,
            bool releaseTransportHandles)
        {
            if (!releaseTransportHandles)
            {
                if (capturedListenerTask != null)
                {
                    ObserveFault(capturedListenerTask);
                }

                return;
            }

            var transportReleased = false;
            var cancellationTask = RequestCancellationAsync(capturedCancellationTokenSource);
            var terminationTask = capturedListenerTask == null
                ? cancellationTask
                : Task.WhenAll(cancellationTask, capturedListenerTask);
            try
            {
                if (releaseTransportHandles)
                {
                    transportReleased = TryReleaseTransportHandles("IPC server failed-start cleanup");
                }

                await WaitForListenerStopAsync(
                        terminationTask,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                lock (syncRoot)
                {
                    isRestartBlocked = true;
                }

                if (releaseTransportHandles)
                {
                    RetainIncompleteTermination(
                        terminationTask,
                        capturedCancellationTokenSource);
                }

                daemonLogger.Error(
                    DaemonLogCategories.Ipc,
                    "IPC server listener did not terminate while cleaning up a failed start. A successor listener will not be started in this Editor process.",
                    exception.ToString());
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
                DisposeCancellationTokenSourceAfterCompletion(
                    capturedCancellationTokenSource,
                    terminationTask);
                if (releaseTransportHandles && !transportReleased)
                {
                    _ = TryReleaseTransportHandles("IPC server failed-start cleanup retry");
                }
            }

        }

        private async Task WaitForListenerStopAsync (
            Task terminationTask,
            CancellationToken cancellationToken)
        {
            using var stopDeadlineCancellationTokenSource = new CancellationTokenSource(listenerStopTimeout);
            using var stopCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                stopDeadlineCancellationTokenSource.Token);
            try
            {
                await CancellationGracePeriodAwaiter.WaitAsync(
                        terminationTask,
                        stopCancellationTokenSource.Token,
                        TimeSpan.Zero)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                stopDeadlineCancellationTokenSource.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested
                && !terminationTask.IsCompleted)
            {
                ObserveFault(terminationTask);
                throw new TimeoutException(
                    $"IPC listener generation did not terminate within {listenerStopTimeout.TotalMilliseconds:0} milliseconds.");
            }
        }

        private static Task RequestCancellationAsync (CancellationTokenSource? cancellationTokenSource)
        {
            if (cancellationTokenSource == null)
            {
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                try
                {
                    cancellationTokenSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            });
        }

        private static void DisposeCancellationTokenSourceAfterCompletion (
            CancellationTokenSource? cancellationTokenSource,
            Task terminationTask)
        {
            _ = terminationTask.ContinueWith(
                completedTask =>
                {
                    _ = completedTask.Exception;
                    cancellationTokenSource?.Dispose();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void RetainIncompleteTermination (
            Task? incompleteListenerTask,
            CancellationTokenSource? incompleteCancellationTokenSource)
        {
            lock (syncRoot)
            {
                if (listenerTask == null && listenerCancellationTokenSource == null)
                {
                    listenerTask = incompleteListenerTask;
                    listenerCancellationTokenSource = incompleteCancellationTokenSource;
                }
            }
        }

        private static void ObserveFault (Task task)
        {
            _ = task.ContinueWith(
                static completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        /// <summary> Releases transport handles used by active listener loops without skipping later listeners after one failure. </summary>
        private bool TryReleaseTransportHandles (string operation)
        {
            var allReleased = true;
            foreach (var listener in transportListeners)
            {
                try
                {
                    listener.Release();
                }
                catch (Exception exception)
                {
                    allReleased = false;
                    daemonLogger.Warning(
                        DaemonLogCategories.Ipc,
                        $"{operation} could not release {listener.TransportKind} transport handles. {exception.Message}");
                }
            }

            return allReleased;
        }

        private sealed class ListenerGenerationPublicationState
        {
            private const int PublicationPhasePending = 0;

            private const int PublicationPhaseCommitted = 1;

            private const int PublicationPhaseTerminated = 2;

            private Task? listenerRunTask;

            private int publicationPhase;

            public bool IsPublicationPending =>
                Volatile.Read(ref publicationPhase) == PublicationPhasePending;

            public bool IsListenerRunning
            {
                get
                {
                    var capturedListenerRunTask = Volatile.Read(ref listenerRunTask);
                    return capturedListenerRunTask != null && !capturedListenerRunTask.IsCompleted;
                }
            }

            public void AttachListenerTask (Task listenerRunTask)
            {
                if (listenerRunTask == null)
                {
                    throw new ArgumentNullException(nameof(listenerRunTask));
                }

                if (Interlocked.CompareExchange(ref this.listenerRunTask, listenerRunTask, null) != null)
                {
                    throw new InvalidOperationException("The listener generation already has a transport task.");
                }
            }

            public void MarkTerminationStarted ()
            {
                _ = Interlocked.CompareExchange(
                    ref publicationPhase,
                    PublicationPhaseTerminated,
                    PublicationPhasePending);
            }

            public bool TryBeginPublicationCommit ()
            {
                var capturedListenerRunTask = Volatile.Read(ref listenerRunTask);
                if (capturedListenerRunTask == null || capturedListenerRunTask.IsCompleted)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(
                        ref publicationPhase,
                        PublicationPhaseCommitted,
                        PublicationPhasePending) != PublicationPhasePending)
                {
                    return false;
                }

                if (!capturedListenerRunTask.IsCompleted)
                {
                    return true;
                }

                _ = Interlocked.CompareExchange(
                    ref publicationPhase,
                    PublicationPhaseTerminated,
                    PublicationPhaseCommitted);
                return false;
            }
        }

        private sealed class ListenerGenerationPublicationFence : IUnityIpcServerPublicationFence
        {
            private readonly object syncRoot = new object();

            private readonly UnityIpcServer server;

            private readonly ListenerGenerationPublicationState publicationState;

            private bool disposed;

            public ListenerGenerationPublicationFence (
                UnityIpcServer server,
                ListenerGenerationPublicationState publicationState)
            {
                this.server = server ?? throw new ArgumentNullException(nameof(server));
                this.publicationState = publicationState ?? throw new ArgumentNullException(nameof(publicationState));
            }

            public void ThrowIfGenerationTerminated ()
            {
                lock (syncRoot)
                {
                    ThrowIfDisposed();
                    server.ThrowIfListenerGenerationTerminated(publicationState);
                }
            }

            public bool TryCommitActiveOwnership (Action commitActiveOwnership)
            {
                if (commitActiveOwnership == null)
                {
                    throw new ArgumentNullException(nameof(commitActiveOwnership));
                }

                lock (syncRoot)
                {
                    ThrowIfDisposed();
                    return server.TryCommitListenerGenerationPublication(
                        publicationState,
                        commitActiveOwnership);
                }
            }

            public void Dispose ()
            {
                lock (syncRoot)
                {
                    disposed = true;
                }
            }

            private void ThrowIfDisposed ()
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(ListenerGenerationPublicationFence));
                }
            }
        }
    }
}
