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
            try
            {
                var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
                var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectRoot);
                var projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectRoot);
                var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, projectFingerprint);
                var sessionOptions = UnityGuiBootstrapSessionOptions.Create(bootstrapArguments);
                var registration = await UnityGuiSessionPersistence.Write(
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

                var serviceProvider = services.BuildServiceProvider();
                var server = serviceProvider.GetRequiredService<IUnityIpcServer>();
                var shutdownSignal = serviceProvider.GetRequiredService<IDaemonShutdownSignal>();
                var unityLogCaptureService = serviceProvider.GetRequiredService<UnityLogCaptureService>();
                unityLogCaptureService.Start();

                await server.Start(endpoint, CancellationToken.None);
                nextState = new ActiveGuiBootstrapState(
                    registration,
                    server,
                    shutdownSignal,
                    unityLogCaptureService,
                    serviceProvider,
                    daemonLogger);
                nextState.RunTask = Monitor(nextState);
                lock (SyncRoot)
                {
                    activeState = nextState;
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
                    await StopState(nextState, requestProcessExit: false);
                }
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

            try
            {
                await state.Server.Stop(CancellationToken.None);
            }
            catch (Exception exception)
            {
                state.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI IPC server stop failed. {exception.Message}");
            }

            try
            {
                state.UnityLogCaptureService.Dispose();
            }
            catch (Exception exception)
            {
                state.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI Unity log capture disposal failed. {exception.Message}");
            }

            try
            {
                UnityGuiSessionPersistence.Delete(state.Registration);
            }
            catch (Exception exception)
            {
                state.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI session cleanup failed. {exception.Message}");
            }

            if (state.ServiceProvider is IDisposable disposableServiceProvider)
            {
                disposableServiceProvider.Dispose();
            }

            if (requestProcessExit)
            {
                EditorApplication.Exit(0);
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

            public Task RunTask { get; set; }

            public bool TryBeginStop ()
            {
                return Interlocked.Exchange(ref stopStarted, 1) == 0;
            }
        }
    }
}
