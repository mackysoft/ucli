using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;
using MackySoft.Ucli.Unity.Runtime;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;
using UnityEngine;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Bootstraps IPC daemon server registration for non-batchmode Unity GUI Editor sessions. </summary>
    internal static class UnityGuiBootstrap
    {
        private static readonly object SyncRoot = new object();

        private static readonly SemaphoreSlim LifecycleGate = new SemaphoreSlim(1, 1);

        private static ActiveGuiBootstrapState activeState;

        private static DateTimeOffset lastLifecycleSidecarWriteUtc;

        /// <summary> Starts or replaces the active GUI daemon session registration. </summary>
        /// <param name="bootstrapArguments"> Optional CLI GUI bootstrap arguments. </param>
        /// <param name="sessionReplacementScope"> The scope of existing current-process GUI sessions that may be replaced. </param>
        /// <returns> A task that produces the GUI endpoint registration result. </returns>
        public static async Task<UnityGuiBootstrapStartResult> StartAsync (
            IpcGuiBootstrapArguments bootstrapArguments,
            UnityGuiSessionReplacementScope sessionReplacementScope)
        {
            ValidateSessionReplacementScope(sessionReplacementScope);
            await LifecycleGate.WaitAsync(CancellationToken.None);
            try
            {
                ActiveGuiBootstrapState capturedState;
                lock (SyncRoot)
                {
                    capturedState = activeState;
                }

                if (capturedState != null)
                {
                    if (!capturedState.ShutdownSignal.IsSignaled)
                    {
                        return UnityGuiBootstrapStartResult.AlreadyRunning();
                    }

                    // NOTE:
                    // daemon stop writes the shutdown response before the monitor clears activeState.
                    // A supervisor rebootstrap can arrive in that small window, so StartAsync owns the
                    // pending stop before creating a replacement daemon endpoint.
                    ClearActiveState(capturedState);
                    await StopStateAsync(capturedState, requestProcessExit: false);
                }

                return await StartUnlockedAsync(
                    bootstrapArguments: bootstrapArguments,
                    sessionReplacementScope: sessionReplacementScope);
            }
            finally
            {
                LifecycleGate.Release();
            }
        }

        private static async Task<UnityGuiBootstrapStartResult> StartUnlockedAsync (
            IpcGuiBootstrapArguments bootstrapArguments,
            UnityGuiSessionReplacementScope sessionReplacementScope)
        {
            var daemonLogStream = new DaemonLogRingBuffer();
            var daemonLogger = new DaemonLogger(daemonLogStream);
            ActiveGuiBootstrapState nextState = null;
            UnityGuiSessionRegistration registration = null;
            IUnityIpcServer server = null;
            UnityLogCaptureService unityLogCaptureService = null;
            IServiceProvider serviceProvider = null;
            try
            {
                var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
                var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectRoot);
                var projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectRoot);
                var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, projectFingerprint);
                var sessionOptions = UnityGuiBootstrapSessionOptions.Create(bootstrapArguments);
                registration = await UnityGuiSessionPersistence.WriteAsync(
                    storageRoot,
                    projectFingerprint,
                    endpoint,
                    sessionOptions,
                    sessionReplacementScope: sessionReplacementScope,
                    cancellationToken: CancellationToken.None);

                var daemonBootstrapArguments = new IpcDaemonBootstrapArguments(
                    RepositoryRoot: storageRoot,
                    ProjectFingerprint: projectFingerprint,
                    SessionPath: registration.SessionPath,
                    SessionIssuedAtUtc: registration.IssuedAtUtc,
                    EndpointTransportKind: ContractLiteralCodec.ToValue(endpoint.TransportKind),
                    EndpointAddress: endpoint.Address);
                var services = new ServiceCollection();
                services
                    .AddUnityIpcApplicationServices(
                        new FileBackedSessionTokenValidator(registration.SessionPath),
                        projectFingerprint,
                        daemonLogger,
                        DaemonEditorMode.Gui)
                    .AddUnityIpcDaemonHostServices(
                        daemonBootstrapArguments,
                        daemonLogStream);

                serviceProvider = services.BuildServiceProvider();
                server = serviceProvider.GetRequiredService<IUnityIpcServer>();
                var readinessGate = serviceProvider.GetRequiredService<IUnityEditorReadinessGate>();
                var shutdownSignal = serviceProvider.GetRequiredService<IDaemonShutdownSignal>();
                var serverVersion = serviceProvider.GetRequiredService<IServerVersionProvider>().GetVersion();
                unityLogCaptureService = serviceProvider.GetRequiredService<UnityLogCaptureService>();
                unityLogCaptureService.Start();

                await server.StartAsync(endpoint, CancellationToken.None);
                var initialSnapshot = readinessGate.CaptureSnapshot();
                UnityLifecycleSidecarPersistence.Write(
                    storageRoot,
                    projectFingerprint,
                    serverVersion,
                    initialSnapshot);
                lastLifecycleSidecarWriteUtc = initialSnapshot.ObservedAtUtc ?? DateTimeOffset.UtcNow;
                nextState = new ActiveGuiBootstrapState(
                    registration,
                    server,
                    shutdownSignal,
                    unityLogCaptureService,
                    serviceProvider,
                    daemonLogger,
                    storageRoot,
                    projectFingerprint,
                    serverVersion,
                    readinessGate);
                lock (SyncRoot)
                {
                    activeState = nextState;
                    _ = MonitorAsync(nextState);
                }

                EnsureEditorLifecycleSubscriptions();
                daemonLogger.Info(
                    DaemonLogCategories.Lifecycle,
                    $"uCLI GUI daemon registered. storageRoot={storageRoot}, fingerprint={projectFingerprint}, endpoint={endpoint.Address}");
                return UnityGuiBootstrapStartResult.Started();
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "uCLI GUI daemon bootstrap failed.",
                    exception);
                Debug.LogException(exception);
                if (nextState != null)
                {
                    ClearActiveState(nextState);
                    await StopStateAsync(nextState, requestProcessExit: false);
                    return UnityGuiBootstrapStartResult.Failure(exception.Message);
                }

                await CleanupFailedStartAsync(
                    registration,
                    server,
                    unityLogCaptureService,
                    serviceProvider,
                    daemonLogger);
                return UnityGuiBootstrapStartResult.Failure(exception.Message);
            }
        }

        private static void ValidateSessionReplacementScope (UnityGuiSessionReplacementScope sessionReplacementScope)
        {
            switch (sessionReplacementScope)
            {
                case UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession:
                case UnityGuiSessionReplacementScope.AnyCurrentProcessSession:
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sessionReplacementScope), sessionReplacementScope, null);
            }
        }

        /// <summary> Stops the active GUI daemon session registration when one exists. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by caller lifecycle. </param>
        /// <returns> A task that completes after active resources have been released. </returns>
        public static async Task StopAsync (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LifecycleGate.WaitAsync(cancellationToken);
            try
            {
                ActiveGuiBootstrapState capturedState;
                lock (SyncRoot)
                {
                    capturedState = activeState;
                    activeState = null;
                }

                if (capturedState == null)
                {
                    return;
                }

                await StopStateAsync(capturedState, requestProcessExit: false);
            }
            finally
            {
                LifecycleGate.Release();
            }
        }

        private static async Task MonitorAsync (ActiveGuiBootstrapState state)
        {
            try
            {
                var shutdownWaitTask = state.ShutdownSignal.WaitAsync(CancellationToken.None);
                var serverTerminationTask = state.Server.WaitForTerminationAsync(CancellationToken.None);
                var completedTask = await Task.WhenAny(shutdownWaitTask, serverTerminationTask);
                if (ReferenceEquals(completedTask, serverTerminationTask))
                {
                    await serverTerminationTask;
                    state.DaemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        "GUI IPC server loop terminated before shutdown signal.");
                    await StopFromMonitorAsync(state, requestProcessExit: false);
                    return;
                }

                await shutdownWaitTask;
                state.DaemonLogger.Info(
                    DaemonLogCategories.Lifecycle,
                    "GUI daemon shutdown signal received. Stopping IPC server and invalidating session.");
                await StopFromMonitorAsync(state, requestProcessExit: state.Registration.CanShutdownProcess);
            }
            catch (Exception exception)
            {
                state.DaemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "GUI daemon monitor failed.",
                    exception);
                Debug.LogException(exception);
            }
        }

        private static async Task StopFromMonitorAsync (
            ActiveGuiBootstrapState state,
            bool requestProcessExit)
        {
            await LifecycleGate.WaitAsync(CancellationToken.None);
            try
            {
                ClearActiveState(state);
                await StopStateAsync(state, requestProcessExit);
            }
            finally
            {
                LifecycleGate.Release();
            }
        }

        private static void ClearActiveState (ActiveGuiBootstrapState state)
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(activeState, state))
                {
                    activeState = null;
                }
            }
        }

        private static async Task StopStateAsync (
            ActiveGuiBootstrapState state,
            bool requestProcessExit)
        {
            await StopStateAsync(state, requestProcessExit, deleteSession: true);
        }

        private static async Task StopStateAsync (
            ActiveGuiBootstrapState state,
            bool requestProcessExit,
            bool deleteSession)
        {
            if (!state.TryBeginStop())
            {
                return;
            }

            await ReleaseResourcesAsync(
                state.Registration,
                state.Server,
                state.UnityLogCaptureService,
                state.ServiceProvider,
                state.DaemonLogger,
                cleanupContext: null,
                deleteSession);

            if (requestProcessExit)
            {
                EditorApplication.Exit(0);
            }
        }

        /// <summary> Releases resources that may have been acquired before GUI bootstrap startup failed. </summary>
        /// <param name="registration"> The persisted session registration when already written. </param>
        /// <param name="server"> The IPC server instance when already resolved. </param>
        /// <param name="unityLogCaptureService"> The Unity log capture service when already resolved. </param>
        /// <param name="serviceProvider"> The service provider when already built. </param>
        /// <param name="daemonLogger"> The logger used to report cleanup failures. </param>
        /// <returns> A task that completes after all available resources are released. </returns>
        internal static async Task CleanupFailedStartAsync (
            UnityGuiSessionRegistration registration,
            IUnityIpcServer server,
            IDisposable unityLogCaptureService,
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger)
        {
            await ReleaseResourcesAsync(
                registration,
                server,
                unityLogCaptureService,
                serviceProvider,
                daemonLogger,
                cleanupContext: "after failed startup",
                deleteSession: true);
        }

        private static async Task ReleaseResourcesAsync (
            UnityGuiSessionRegistration registration,
            IUnityIpcServer server,
            IDisposable unityLogCaptureService,
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger,
            string cleanupContext,
            bool deleteSession)
        {
            daemonLogger ??= NoOpDaemonLogger.Instance;
            if (server != null)
            {
                try
                {
                    await server.StopAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        FormatCleanupFailureMessage("GUI IPC server stop", cleanupContext, exception));
                }
            }

            DisposeUnityLogCapture(unityLogCaptureService, daemonLogger, cleanupContext);
            DeleteSession(registration, daemonLogger, cleanupContext, deleteSession);
            DisposeServiceProvider(serviceProvider, daemonLogger, cleanupContext);
        }

        /// <summary> Releases resources from Unity lifecycle callbacks without blocking editor close or domain reload. </summary>
        /// <param name="registration"> The persisted session registration when available. </param>
        /// <param name="server"> The IPC server instance when available. </param>
        /// <param name="unityLogCaptureService"> The Unity log capture service when available. </param>
        /// <param name="serviceProvider"> The service provider when available. </param>
        /// <param name="daemonLogger"> The logger used to report cleanup failures. </param>
        /// <param name="deleteSession"> Whether persisted session metadata should be deleted. </param>
        internal static void ReleaseResourcesForEditorLifecycleEvent (
            UnityGuiSessionRegistration registration,
            IUnityIpcServer server,
            IDisposable unityLogCaptureService,
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger,
            bool deleteSession)
        {
            daemonLogger ??= NoOpDaemonLogger.Instance;
            const string CleanupContext = "during editor lifecycle event";

            // NOTE:
            // EditorApplication.quitting and beforeAssemblyReload are synchronous Unity main-thread callbacks.
            // They must release OS transport handles on this callback thread, but must not wait for accepted
            // connection tasks. Domain reload can tear down those tasks while Unity is rebuilding assemblies;
            // joining them here can block the next GUI daemon bootstrap.
            if (server != null)
            {
                try
                {
                    server.ReleaseForEditorLifecycleEvent();
                }
                catch (Exception exception)
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        FormatCleanupFailureMessage("GUI IPC server lifecycle release", CleanupContext, exception));
                }
            }

            DisposeUnityLogCapture(unityLogCaptureService, daemonLogger, CleanupContext);
            DeleteSession(registration, daemonLogger, CleanupContext, deleteSession);
            DisposeServiceProvider(serviceProvider, daemonLogger, CleanupContext);
        }

        private static void DisposeUnityLogCapture (
            IDisposable unityLogCaptureService,
            IDaemonLogger daemonLogger,
            string cleanupContext)
        {
            if (unityLogCaptureService == null)
            {
                return;
            }

            try
            {
                unityLogCaptureService.Dispose();
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    FormatCleanupFailureMessage("GUI Unity log capture disposal", cleanupContext, exception));
            }
        }

        private static void DeleteSession (
            UnityGuiSessionRegistration registration,
            IDaemonLogger daemonLogger,
            string cleanupContext,
            bool deleteSession)
        {
            if (registration == null || !deleteSession)
            {
                return;
            }

            try
            {
                UnityGuiSessionPersistence.Delete(registration);
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    FormatCleanupFailureMessage("GUI session cleanup", cleanupContext, exception));
            }
        }

        private static void DisposeServiceProvider (
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger,
            string cleanupContext)
        {
            if (serviceProvider is IDisposable disposableServiceProvider)
            {
                try
                {
                    disposableServiceProvider.Dispose();
                }
                catch (Exception exception)
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        FormatCleanupFailureMessage("GUI service provider disposal", cleanupContext, exception));
                }
            }
        }

        private static string FormatCleanupFailureMessage (
            string operation,
            string cleanupContext,
            Exception exception)
        {
            return string.IsNullOrEmpty(cleanupContext)
                ? $"{operation} failed. {exception.Message}"
                : $"{operation} {cleanupContext} failed. {exception.Message}";
        }

        private static void EnsureEditorLifecycleSubscriptions ()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= StopForDomainReloadSynchronously;
            EditorApplication.playModeStateChanged -= PersistLifecycleSidecarOnPlayModeStateChanged;
            EditorApplication.update -= PersistLifecycleSidecarOnEditorUpdate;
            EditorApplication.quitting -= StopSynchronously;
            AssemblyReloadEvents.beforeAssemblyReload += StopForDomainReloadSynchronously;
            EditorApplication.playModeStateChanged += PersistLifecycleSidecarOnPlayModeStateChanged;
            EditorApplication.update += PersistLifecycleSidecarOnEditorUpdate;
            EditorApplication.quitting += StopSynchronously;
        }

        private static void PersistLifecycleSidecarOnPlayModeStateChanged (PlayModeStateChange _)
        {
            PersistActiveLifecycleSidecar(force: true);
        }

        private static void PersistLifecycleSidecarOnEditorUpdate ()
        {
            PersistActiveLifecycleSidecar(force: false);
        }

        private static void PersistActiveLifecycleSidecar (bool force)
        {
            var now = DateTimeOffset.UtcNow;
            if (!force && now - lastLifecycleSidecarWriteUtc < DaemonLifecycleObservationTimings.SidecarRefreshInterval)
            {
                return;
            }

            ActiveGuiBootstrapState capturedState;
            lock (SyncRoot)
            {
                capturedState = activeState;
            }

            if (capturedState == null || capturedState.ShutdownSignal.IsSignaled)
            {
                return;
            }

            try
            {
                // NOTE:
                // CLI status commands may time out while Unity is in Play Mode or recovering, but Unity main-thread
                // callbacks still own the authoritative lifecycle snapshot. Keep the sidecar fresh enough to serve as
                // the read-only observation path without moving Unity API access off the main thread.
                var snapshot = capturedState.ReadinessGate.CaptureSnapshot() with
                {
                    ObservedAtUtc = now,
                };
                UnityLifecycleSidecarPersistence.Write(
                    capturedState.StorageRoot,
                    capturedState.ProjectFingerprint,
                    capturedState.ServerVersion,
                    snapshot);
                lastLifecycleSidecarWriteUtc = now;
            }
            catch (Exception exception)
            {
                capturedState.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI lifecycle sidecar refresh failed. {exception.Message}");
            }
        }

        private static void StopSynchronously ()
        {
            ActiveGuiBootstrapState capturedState;
            lock (SyncRoot)
            {
                capturedState = activeState;
                activeState = null;
            }

            if (capturedState == null || !capturedState.TryBeginStop())
            {
                return;
            }

            ReleaseResourcesForEditorLifecycleEvent(
                capturedState.Registration,
                capturedState.Server,
                capturedState.UnityLogCaptureService,
                capturedState.ServiceProvider,
                capturedState.DaemonLogger,
                deleteSession: true);
        }

        private static void StopForDomainReloadSynchronously ()
        {
            ActiveGuiBootstrapState capturedState;
            lock (SyncRoot)
            {
                capturedState = activeState;
                activeState = null;
            }

            if (capturedState == null)
            {
                return;
            }

            try
            {
                var snapshot = capturedState.ReadinessGate.CaptureSnapshot() with
                {
                    LifecycleState = IpcEditorLifecycleStateCodec.Recovering,
                    BlockingReason = UnityEditorExecutionReadinessPolicy.ResolveBlockingReason(IpcEditorLifecycleStateCodec.Recovering),
                    CanAcceptExecutionRequests = false,
                    ObservedAtUtc = DateTimeOffset.UtcNow,
                };
                UnityLifecycleSidecarPersistence.Write(
                    capturedState.StorageRoot,
                    capturedState.ProjectFingerprint,
                    capturedState.ServerVersion,
                    snapshot);
            }
            catch (Exception exception)
            {
                capturedState.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI lifecycle recovery sidecar write failed before domain reload. {exception.Message}");
            }

            if (!capturedState.TryBeginStop())
            {
                return;
            }

            ReleaseResourcesForEditorLifecycleEvent(
                capturedState.Registration,
                capturedState.Server,
                capturedState.UnityLogCaptureService,
                capturedState.ServiceProvider,
                capturedState.DaemonLogger,
                deleteSession: false);
        }

        private sealed class ActiveGuiBootstrapState
        {
            private int stopStarted;

            public ActiveGuiBootstrapState (
                UnityGuiSessionRegistration registration,
                IUnityIpcServer server,
                IDaemonShutdownSignal shutdownSignal,
                UnityLogCaptureService unityLogCaptureService,
                IServiceProvider serviceProvider,
                IDaemonLogger daemonLogger,
                string storageRoot,
                string projectFingerprint,
                string serverVersion,
                IUnityEditorReadinessGate readinessGate)
            {
                Registration = registration ?? throw new ArgumentNullException(nameof(registration));
                Server = server ?? throw new ArgumentNullException(nameof(server));
                ShutdownSignal = shutdownSignal ?? throw new ArgumentNullException(nameof(shutdownSignal));
                UnityLogCaptureService = unityLogCaptureService ?? throw new ArgumentNullException(nameof(unityLogCaptureService));
                ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                DaemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
                StorageRoot = storageRoot ?? throw new ArgumentNullException(nameof(storageRoot));
                ProjectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));
                ServerVersion = serverVersion ?? throw new ArgumentNullException(nameof(serverVersion));
                ReadinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            }

            public UnityGuiSessionRegistration Registration { get; }

            public IUnityIpcServer Server { get; }

            public IDaemonShutdownSignal ShutdownSignal { get; }

            public UnityLogCaptureService UnityLogCaptureService { get; }

            public IServiceProvider ServiceProvider { get; }

            public IDaemonLogger DaemonLogger { get; }

            public string StorageRoot { get; }

            public string ProjectFingerprint { get; }

            public string ServerVersion { get; }

            public IUnityEditorReadinessGate ReadinessGate { get; }

            public bool TryBeginStop ()
            {
                return Interlocked.Exchange(ref stopStarted, 1) == 0;
            }
        }
    }
}
