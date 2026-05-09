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

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Bootstraps IPC daemon server registration for non-batchmode Unity GUI Editor sessions. </summary>
    internal static class UnityGuiBootstrap
    {
        private static readonly object SyncRoot = new object();

        private static ActiveGuiBootstrapState activeState;

        /// <summary> Starts or replaces the active GUI daemon session registration. </summary>
        /// <param name="bootstrapArguments"> Optional CLI GUI bootstrap arguments. </param>
        /// <returns> A task that completes when the GUI endpoint has been registered. </returns>
        public static async Task StartAsync (IpcGuiBootstrapArguments bootstrapArguments)
        {
            await StopAsync(CancellationToken.None);
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
                    CancellationToken.None);

                var daemonBootstrapArguments = new IpcDaemonBootstrapArguments(
                    RepositoryRoot: storageRoot,
                    ProjectFingerprint: projectFingerprint,
                    SessionPath: registration.SessionPath,
                    SessionIssuedAtUtc: registration.IssuedAtUtc,
                    EndpointTransportKind: IpcTransportKindCodec.ToValue(endpoint.TransportKind),
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
                unityLogCaptureService = serviceProvider.GetRequiredService<UnityLogCaptureService>();
                unityLogCaptureService.Start();

                await server.StartAsync(endpoint, CancellationToken.None);
                UnityLifecycleSidecarPersistence.Write(
                    storageRoot,
                    projectFingerprint,
                    readinessGate.CaptureSnapshot());
                nextState = new ActiveGuiBootstrapState(
                    registration,
                    server,
                    shutdownSignal,
                    unityLogCaptureService,
                    serviceProvider,
                    daemonLogger,
                    storageRoot,
                    projectFingerprint,
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
                    return;
                }

                await CleanupFailedStartAsync(
                    registration,
                    server,
                    unityLogCaptureService,
                    serviceProvider,
                    daemonLogger);
            }
        }

        /// <summary> Stops the active GUI daemon session registration when one exists. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by caller lifecycle. </param>
        /// <returns> A task that completes after active resources have been released. </returns>
        public static async Task StopAsync (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                    ClearActiveState(state);
                    await StopStateAsync(state, requestProcessExit: false);
                    return;
                }

                await shutdownWaitTask;
                state.DaemonLogger.Info(
                    DaemonLogCategories.Lifecycle,
                    "GUI daemon shutdown signal received. Stopping IPC server and invalidating session.");
                ClearActiveState(state);
                await StopStateAsync(state, requestProcessExit: state.Registration.CanShutdownProcess);
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

            if (unityLogCaptureService != null)
            {
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

            if (registration != null && deleteSession)
            {
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
            EditorApplication.quitting -= StopSynchronously;
            AssemblyReloadEvents.beforeAssemblyReload += StopForDomainReloadSynchronously;
            EditorApplication.quitting += StopSynchronously;
        }

        private static void StopSynchronously ()
        {
            StopAsync(CancellationToken.None).GetAwaiter().GetResult();
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
                    snapshot);
            }
            catch (Exception exception)
            {
                capturedState.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI lifecycle recovery sidecar write failed before domain reload. {exception.Message}");
            }

            StopStateAsync(capturedState, requestProcessExit: false, deleteSession: false).GetAwaiter().GetResult();
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

            public IUnityEditorReadinessGate ReadinessGate { get; }

            public bool TryBeginStop ()
            {
                return Interlocked.Exchange(ref stopStarted, 1) == 0;
            }
        }
    }
}
