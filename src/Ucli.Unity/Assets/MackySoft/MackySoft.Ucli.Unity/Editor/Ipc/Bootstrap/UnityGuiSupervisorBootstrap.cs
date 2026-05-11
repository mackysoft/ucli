using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
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
    /// <summary> Hosts the GUI-side supervisor endpoint that survives daemon endpoint shutdown. </summary>
    internal static class UnityGuiSupervisorBootstrap
    {
        private static readonly object SyncRoot = new object();

        private static readonly SemaphoreSlim StartGate = new SemaphoreSlim(1, 1);

        private static ActiveGuiSupervisorState activeState;

        public static async Task StartAsync ()
        {
            ActiveGuiSupervisorState capturedState;
            lock (SyncRoot)
            {
                capturedState = activeState;
            }

            if (capturedState != null)
            {
                return;
            }

            await StartGate.WaitAsync(CancellationToken.None);
            var daemonLogger = new DaemonLogger(new DaemonLogRingBuffer());
            IUnityIpcServer server = null;
            IServiceProvider serviceProvider = null;
            ActiveGuiSupervisorState nextState = null;
            string startupStorageRoot = null;
            string startupProjectFingerprint = null;
            try
            {
                lock (SyncRoot)
                {
                    if (activeState != null)
                    {
                        return;
                    }
                }

                var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
                var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectRoot);
                var projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectRoot);
                startupStorageRoot = storageRoot;
                startupProjectFingerprint = projectFingerprint;
                var endpoint = UcliIpcEndpointResolver.ResolveGuiSupervisorEndpoint(storageRoot, projectFingerprint);
                var sessionToken = Guid.NewGuid().ToString("N");
                var manifest = UnityGuiSupervisorPersistence.Write(
                    storageRoot,
                    projectFingerprint,
                    endpoint,
                    sessionToken,
                    DateTimeOffset.UtcNow);

                var services = new ServiceCollection();
                services.AddUnityGuiSupervisorHostServices(
                    new ExactSessionTokenValidator(sessionToken),
                    projectFingerprint,
                    daemonLogger);
                serviceProvider = services.BuildServiceProvider();
                server = serviceProvider.GetRequiredService<IUnityIpcServer>();
                await server.StartAsync(endpoint, CancellationToken.None);

                nextState = new ActiveGuiSupervisorState(
                    manifest,
                    server,
                    serviceProvider,
                    daemonLogger,
                    storageRoot,
                    projectFingerprint);
                lock (SyncRoot)
                {
                    activeState = nextState;
                    _ = MonitorAsync(nextState);
                }

                EnsureEditorLifecycleSubscriptions();
                daemonLogger.Info(
                    DaemonLogCategories.Lifecycle,
                    $"uCLI GUI supervisor registered. storageRoot={storageRoot}, fingerprint={projectFingerprint}, endpoint={endpoint.Address}");
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "uCLI GUI supervisor bootstrap failed.",
                    exception);
                Debug.LogException(exception);
                if (nextState != null)
                {
                    ClearActiveState(nextState);
                    await StopStateAsync(nextState);
                    return;
                }

                await CleanupFailedStartAsync(server, serviceProvider, daemonLogger);
                DeleteManifestAfterFailedStart(startupStorageRoot, startupProjectFingerprint, daemonLogger);
            }
            finally
            {
                StartGate.Release();
            }
        }

        private static async Task MonitorAsync (ActiveGuiSupervisorState state)
        {
            try
            {
                await state.Server.WaitForTerminationAsync(CancellationToken.None);
                state.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    "GUI supervisor IPC server loop terminated.");
                ClearActiveState(state);
                await StopStateAsync(state);
            }
            catch (Exception exception)
            {
                state.DaemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "GUI supervisor monitor failed.",
                    exception);
                Debug.LogException(exception);
            }
        }

        private static void ClearActiveState (ActiveGuiSupervisorState state)
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(activeState, state))
                {
                    activeState = null;
                }
            }
        }

        private static async Task StopStateAsync (ActiveGuiSupervisorState state)
        {
            if (!state.TryBeginStop())
            {
                return;
            }

            try
            {
                await state.Server.StopAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                state.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI supervisor IPC server stop failed. {exception.Message}");
            }

            ReleaseStateResources(state);
        }

        private static async Task CleanupFailedStartAsync (
            IUnityIpcServer server,
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger)
        {
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
                        $"GUI supervisor failed-start server stop failed. {exception.Message}");
                }
            }

            DisposeServiceProvider(serviceProvider, daemonLogger);
        }

        private static void DeleteManifestAfterFailedStart (
            string storageRoot,
            string projectFingerprint,
            IDaemonLogger daemonLogger)
        {
            if (string.IsNullOrWhiteSpace(storageRoot) || string.IsNullOrWhiteSpace(projectFingerprint))
            {
                return;
            }

            try
            {
                UnityGuiSupervisorPersistence.Delete(storageRoot, projectFingerprint);
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI supervisor manifest cleanup after failed startup failed. {exception.Message}");
            }
        }

        private static void ReleaseStateForEditorLifecycleEvent (ActiveGuiSupervisorState state)
        {
            if (!state.TryBeginStop())
            {
                return;
            }

            _ = state.Server.StopAsync(CancellationToken.None);
            ReleaseStateResources(state);
        }

        private static void ReleaseStateResources (ActiveGuiSupervisorState state)
        {
            try
            {
                UnityGuiSupervisorPersistence.Delete(state.StorageRoot, state.ProjectFingerprint);
            }
            catch (Exception exception)
            {
                state.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI supervisor manifest cleanup failed. {exception.Message}");
            }

            DisposeServiceProvider(state.ServiceProvider, state.DaemonLogger);
        }

        private static void DisposeServiceProvider (
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger)
        {
            if (serviceProvider is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception exception)
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        $"GUI supervisor service provider disposal failed. {exception.Message}");
                }
            }
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
            ActiveGuiSupervisorState capturedState;
            lock (SyncRoot)
            {
                capturedState = activeState;
                activeState = null;
            }

            if (capturedState == null)
            {
                return;
            }

            ReleaseStateForEditorLifecycleEvent(capturedState);
        }

        private sealed class ActiveGuiSupervisorState
        {
            private int stopStarted;

            public ActiveGuiSupervisorState (
                GuiSupervisorManifestJsonContract manifest,
                IUnityIpcServer server,
                IServiceProvider serviceProvider,
                IDaemonLogger daemonLogger,
                string storageRoot,
                string projectFingerprint)
            {
                Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
                Server = server ?? throw new ArgumentNullException(nameof(server));
                ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                DaemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
                StorageRoot = storageRoot ?? throw new ArgumentNullException(nameof(storageRoot));
                ProjectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));
            }

            public GuiSupervisorManifestJsonContract Manifest { get; }

            public IUnityIpcServer Server { get; }

            public IServiceProvider ServiceProvider { get; }

            public IDaemonLogger DaemonLogger { get; }

            public string StorageRoot { get; }

            public string ProjectFingerprint { get; }

            public bool TryBeginStop ()
            {
                return Interlocked.Exchange(ref stopStarted, 1) == 0;
            }
        }
    }
}
