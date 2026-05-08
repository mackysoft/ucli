using System;
using System.Threading;
using System.Threading.Tasks;
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
        public static async Task Start (IpcGuiBootstrapArguments bootstrapArguments)
        {
            await Stop(CancellationToken.None);
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
                registration = await UnityGuiSessionPersistence.Write(
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
                        IpcEditorRuntimeCodec.Gui)
                    .AddUnityIpcDaemonHostServices(
                        daemonBootstrapArguments,
                        daemonLogStream);

                serviceProvider = services.BuildServiceProvider();
                server = serviceProvider.GetRequiredService<IUnityIpcServer>();
                var shutdownSignal = serviceProvider.GetRequiredService<IDaemonShutdownSignal>();
                unityLogCaptureService = serviceProvider.GetRequiredService<UnityLogCaptureService>();
                unityLogCaptureService.Start();

                await server.Start(endpoint, CancellationToken.None);
                nextState = new ActiveGuiBootstrapState(
                    registration,
                    server,
                    shutdownSignal,
                    unityLogCaptureService,
                    serviceProvider,
                    daemonLogger);
                lock (SyncRoot)
                {
                    activeState = nextState;
                    _ = Monitor(nextState);
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
                    await StopState(nextState, requestProcessExit: false);
                    return;
                }

                await CleanupFailedStart(
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
        public static async Task Stop (CancellationToken cancellationToken)
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

            await StopState(capturedState, requestProcessExit: false);
        }

        private static async Task Monitor (ActiveGuiBootstrapState state)
        {
            try
            {
                var shutdownWaitTask = state.ShutdownSignal.Wait(CancellationToken.None);
                var serverTerminationTask = state.Server.WaitForTermination(CancellationToken.None);
                var completedTask = await Task.WhenAny(shutdownWaitTask, serverTerminationTask);
                if (ReferenceEquals(completedTask, serverTerminationTask))
                {
                    await serverTerminationTask;
                    state.DaemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        "GUI IPC server loop terminated before shutdown signal.");
                    ClearActiveState(state);
                    await StopState(state, requestProcessExit: false);
                    return;
                }

                await shutdownWaitTask;
                state.DaemonLogger.Info(
                    DaemonLogCategories.Lifecycle,
                    "GUI daemon shutdown signal received. Stopping IPC server and invalidating session.");
                ClearActiveState(state);
                await StopState(state, requestProcessExit: state.Registration.CanShutdownProcess);
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

        private static async Task StopState (
            ActiveGuiBootstrapState state,
            bool requestProcessExit)
        {
            if (!state.TryBeginStop())
            {
                return;
            }

            await ReleaseResources(
                state.Registration,
                state.Server,
                state.UnityLogCaptureService,
                state.ServiceProvider,
                state.DaemonLogger,
                cleanupContext: null);

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
        internal static async Task CleanupFailedStart (
            UnityGuiSessionRegistration registration,
            IUnityIpcServer server,
            IDisposable unityLogCaptureService,
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger)
        {
            await ReleaseResources(
                registration,
                server,
                unityLogCaptureService,
                serviceProvider,
                daemonLogger,
                cleanupContext: "after failed startup");
        }

        private static async Task ReleaseResources (
            UnityGuiSessionRegistration registration,
            IUnityIpcServer server,
            IDisposable unityLogCaptureService,
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger,
            string cleanupContext)
        {
            daemonLogger ??= NoOpDaemonLogger.Instance;
            if (server != null)
            {
                try
                {
                    await server.Stop(CancellationToken.None);
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

            if (registration != null)
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
            AssemblyReloadEvents.beforeAssemblyReload -= StopSynchronously;
            EditorApplication.quitting -= StopSynchronously;
            AssemblyReloadEvents.beforeAssemblyReload += StopSynchronously;
            EditorApplication.quitting += StopSynchronously;
        }

        private static void StopSynchronously ()
        {
            Stop(CancellationToken.None).GetAwaiter().GetResult();
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
                IDaemonLogger daemonLogger)
            {
                Registration = registration ?? throw new ArgumentNullException(nameof(registration));
                Server = server ?? throw new ArgumentNullException(nameof(server));
                ShutdownSignal = shutdownSignal ?? throw new ArgumentNullException(nameof(shutdownSignal));
                UnityLogCaptureService = unityLogCaptureService ?? throw new ArgumentNullException(nameof(unityLogCaptureService));
                ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                DaemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
            }

            public UnityGuiSessionRegistration Registration { get; }

            public IUnityIpcServer Server { get; }

            public IDaemonShutdownSignal ShutdownSignal { get; }

            public UnityLogCaptureService UnityLogCaptureService { get; }

            public IServiceProvider ServiceProvider { get; }

            public IDaemonLogger DaemonLogger { get; }

            public bool TryBeginStop ()
            {
                return Interlocked.Exchange(ref stopStarted, 1) == 0;
            }
        }
    }
}
