using System;
using System.IO;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Build;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Index;
using MackySoft.Ucli.Unity.Project;
using MackySoft.Ucli.Unity.Runtime;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Registers shared Unity IPC application services used by daemon and oneshot hosts. </summary>
    internal static class UnityIpcServiceCollectionExtensions
    {
        private const int MaximumActiveTransportConnections = 32;

        private static readonly TimeSpan ConnectionDrainTimeout = TimeSpan.FromSeconds(1);

        /// <summary> Registers shared IPC application services and method handlers. </summary>
        /// <param name="services"> The target service collection. </param>
        /// <param name="sessionTokenValidator"> The session-token validator used by the host. </param>
        /// <param name="projectFingerprint"> The project fingerprint served by the host. </param>
        /// <param name="daemonLogger"> The daemon logger used by the host. </param>
        /// <param name="editorMode"> The daemon Editor mode reported by lifecycle snapshots. </param>
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityIpcApplicationServices (
            this IServiceCollection services,
            ISessionTokenValidator sessionTokenValidator,
            string projectFingerprint,
            IDaemonLogger daemonLogger,
            DaemonEditorMode editorMode)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (sessionTokenValidator == null)
            {
                throw new ArgumentNullException(nameof(sessionTokenValidator));
            }

            if (daemonLogger == null)
            {
                throw new ArgumentNullException(nameof(daemonLogger));
            }

            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("projectFingerprint must not be empty.", nameof(projectFingerprint));
            }

            // NOTE:
            // Project owner exposes static helpers only, so service composition starts from Runtime.
            services.AddUnityRuntimeServices(editorMode);
            services.AddUnityIndexServices();
            services.AddUnityExecutionServices();
            services.AddSingleton(CreateProjectIdentity(projectFingerprint));
            services.AddSingleton<ISessionTokenValidator>(sessionTokenValidator);
            services.AddSingleton<IDaemonLogger>(daemonLogger);
            services.AddSingleton<UnityLogRedactionScopeProvider>();
            services.AddSingleton<IUnityLogStream, UnityLogRingBuffer>();
            services.AddSingleton<IEditorLogRangeExporter, EditorLogRangeExporter>();
            services.AddSingleton<IUnityTestRunRequestContextFactory, UnityTestRunRequestContextFactory>();
            services.AddSingleton<IUnityTestRunner, UnityTestRunner>();
            services.AddSingleton<IUnityTestResultsXmlWriter, UnityTestResultsXmlWriter>();
            services.AddSingleton<IUnityTestRunService, UnityTestRunService>();
            services.AddSingleton<IIpcRequestTimeoutScopeFactory, IpcRequestTimeoutScopeFactory>();
            services.AddSingleton<IServerVersionProvider, AssemblyServerVersionProvider>();
            services.AddSingleton<IUnityEditorUpdateAwaiter, UnityEditorUpdateAwaiterAdapter>();
            services.AddSingleton<IUnityPlayModeController, UnityEditorPlayModeController>();
            services.AddSingleton<IUnityBuildTargetSupportProbe, UnityBuildTargetSupportProbe>();
            services.AddSingleton<IUnityBuildPipelineRunner, UnityBuildPipelineRunner>();
#if UNITY_6000_0_OR_NEWER
            services.AddSingleton<IUnityBuildProfileInputResolver, Unity6000BuildProfileInputResolver>();
            services.AddSingleton<IUnityBuildProfileBuildRunner, Unity6000BuildProfileBuildRunner>();
#else
            services.AddSingleton<IUnityBuildProfileInputResolver, UnsupportedUnityBuildProfileInputResolver>();
            services.AddSingleton<IUnityBuildProfileBuildRunner, UnsupportedUnityBuildProfileBuildRunner>();
#endif
            services.AddSingleton<BuildExecuteMethodResolver>();
            services.AddSingleton<BuildExecuteMethodRunner>();
            services.AddSingleton<UnityBuildPreconditionProbe>();
            services.AddSingleton<UnityProjectMutationAuditProbe>();
            services.AddSingleton<PlayEnterTransitionRunner>();
            services.AddSingleton<PlayExitTransitionRunner>();
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider =>
            {
                return new PingUnityIpcMethodHandler(
                    serviceProvider.GetRequiredService<IServerVersionProvider>(),
                    serviceProvider.GetRequiredService<IUnityEditorReadinessGate>(),
                    projectFingerprint,
                    serviceProvider.GetRequiredService<IDaemonLogger>());
            });
            services.AddSingleton<IUnityIpcMethodHandler, ExecuteUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider =>
            {
                return new CompileUnityIpcMethodHandler(
                    serviceProvider.GetRequiredService<IUnityEditorReadinessGate>(),
                    serviceProvider.GetRequiredService<IpcProjectIdentity>(),
                    serviceProvider.GetRequiredService<IServerVersionProvider>(),
                    serviceProvider.GetRequiredService<IDaemonLogger>());
            });
            services.AddSingleton<IUnityIpcMethodHandler, BuildRunUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider =>
            {
                return new PlayStatusUnityIpcMethodHandler(
                    serviceProvider.GetRequiredService<IServerVersionProvider>(),
                    serviceProvider.GetRequiredService<IUnityEditorReadinessGate>(),
                    serviceProvider.GetRequiredService<IpcProjectIdentity>(),
                    serviceProvider.GetRequiredService<IDaemonLogger>());
            });
            services.AddSingleton<IUnityIpcMethodHandler, PlayEnterUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, PlayExitUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, TestRunUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, OpsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, IndexAssetsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, IndexSceneTreeLiteReadUnityIpcMethodHandler>();
            return services;
        }

        private static IpcProjectIdentity CreateProjectIdentity (string projectFingerprint)
        {
            var projectPath = Path.GetFullPath(UnityProjectPathResolver.ResolveProjectRootPath());
            var unityVersion = string.IsNullOrWhiteSpace(Application.unityVersion)
                ? "unknown"
                : Application.unityVersion;
            return new IpcProjectIdentity(
                ProjectPath: projectPath,
                ProjectFingerprint: projectFingerprint,
                UnityVersion: unityVersion);
        }

        /// <summary> Registers daemon-only transport, logging, and lifetime services. </summary>
        /// <param name="services"> The target service collection. </param>
        /// <param name="bootstrapArguments"> The daemon bootstrap arguments. </param>
        /// <param name="daemonLogStream"> The daemon log stream. </param>
        /// <param name="editorInstanceId"> The non-empty Editor process identity captured for this host generation. </param>
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityIpcDaemonHostServices (
            this IServiceCollection services,
            IpcDaemonBootstrapArguments bootstrapArguments,
            IDaemonLogStream daemonLogStream,
            Guid editorInstanceId)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            if (daemonLogStream == null)
            {
                throw new ArgumentNullException(nameof(daemonLogStream));
            }

            if (editorInstanceId == Guid.Empty)
            {
                throw new ArgumentException("Editor instance identifier must not be empty.", nameof(editorInstanceId));
            }

            services.AddSingleton(bootstrapArguments);
            services.AddSingleton<IDaemonLogStream>(daemonLogStream);
            services.AddSingleton<IRecoverableIpcOperationStore>(serviceProvider =>
                FileRecoverableIpcOperationStore.Create(
                    serviceProvider.GetRequiredService<IpcProjectIdentity>(),
                    editorInstanceId));
            services.AddSingleton<IUnityIpcMethodDispatcher>(serviceProvider => CreateMethodDispatcher(
                serviceProvider,
                serviceProvider.GetRequiredService<IRecoverableIpcOperationStore>()));
            services.AddSingleton<IUnityIpcRequestHandler, UnityIpcRequestHandler>();
            services.AddSingleton<UnityCompileMessageDedupeCache>();
            services.AddSingleton<UnityLogCollector>();
            services.AddSingleton<UnityLogCaptureService>();
            services.AddSingleton<IDaemonShutdownSignal, DaemonShutdownSignal>();
            services.AddSingleton<IUnityShutdownAdmissionCoordinator, UnityShutdownAdmissionCoordinator>();
            services.AddSingleton<IDaemonLogsReadRequestValidator, DaemonLogsReadRequestValidator>();
            services.AddSingleton<IDaemonLogsReadQueryEngine, DaemonLogsReadQueryEngine>();
            services.AddSingleton<DaemonLogsReadResponseFactory>();
            services.AddSingleton<UnityLogsReadRequestValidator>();
            services.AddSingleton<UnityLogsReadQueryEngine>();
            services.AddSingleton<UnityLogsReadResponseFactory>();
            services.AddSingleton<IUnityConsoleClearer, UnityEditorConsoleClearer>();
            services.AddSingleton<IUnityIpcMethodHandler, DaemonLogsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, UnityLogsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, UnityConsoleClearUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, ShutdownUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcConnectionHandler>(CreateConnectionHandler);
            AddTransportListeners(services);
            services.AddSingleton<IUnityIpcServer>(serviceProvider =>
            {
                return new UnityIpcServer(
                    serviceProvider.GetRequiredService<IUnityIpcConnectionHandler>(),
                    new IUnityIpcTransportListener[]
                    {
                        serviceProvider.GetRequiredService<NamedPipeUnityIpcTransportListener>(),
                        serviceProvider.GetRequiredService<UnixDomainSocketUnityIpcTransportListener>(),
                    },
                    serviceProvider.GetRequiredService<IDaemonShutdownSignal>(),
                    serviceProvider.GetRequiredService<IDaemonLogger>(),
                    UnityIpcServer.DefaultListenerStopTimeout);
            });
            return services;
        }

        /// <summary> Registers oneshot-only transport and completion services. </summary>
        /// <param name="services"> The target service collection. </param>
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityIpcOneshotHostServices (this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IDaemonShutdownSignal, DaemonShutdownSignal>();
            services.AddSingleton<IUnityShutdownAdmissionCoordinator, UnityShutdownAdmissionCoordinator>();
            services.AddSingleton<IUnityIpcMethodDispatcher>(serviceProvider => CreateMethodDispatcher(
                serviceProvider,
                recoverableOperationStore: null));
            services.AddSingleton<IUnityIpcRequestHandler, UnityIpcRequestHandler>();
            services.AddSingleton<OneshotRequestCompletionSignal>();
            services.AddSingleton<IUnityIpcMethodHandler, ShutdownUnityIpcMethodHandler>();
            services.AddSingleton(CreateConnectionHandler);
            services.AddSingleton<IUnityIpcConnectionHandler, UnityOneshotConnectionHandler>();
            AddTransportListeners(services);
            services.AddSingleton<IUnityIpcServer>(serviceProvider =>
            {
                return new UnityIpcServer(
                    serviceProvider.GetRequiredService<IUnityIpcConnectionHandler>(),
                    new IUnityIpcTransportListener[]
                    {
                        serviceProvider.GetRequiredService<NamedPipeUnityIpcTransportListener>(),
                        serviceProvider.GetRequiredService<UnixDomainSocketUnityIpcTransportListener>(),
                    },
                    serviceProvider.GetRequiredService<IDaemonShutdownSignal>(),
                    serviceProvider.GetRequiredService<IDaemonLogger>(),
                    UnityIpcServer.DefaultListenerStopTimeout);
            });
            return services;
        }

        /// <summary> Registers GUI-supervisor transport and rebootstrap services. </summary>
        /// <param name="services"> The target service collection. </param>
        /// <param name="sessionTokenValidator"> The supervisor-token validator used by the host. </param>
        /// <param name="projectFingerprint"> The project fingerprint served by this GUI supervisor. </param>
        /// <param name="daemonLogger"> The daemon logger used by the host. </param>
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityGuiSupervisorHostServices (
            this IServiceCollection services,
            ISessionTokenValidator sessionTokenValidator,
            string projectFingerprint,
            IDaemonLogger daemonLogger)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (sessionTokenValidator == null)
            {
                throw new ArgumentNullException(nameof(sessionTokenValidator));
            }

            if (daemonLogger == null)
            {
                throw new ArgumentNullException(nameof(daemonLogger));
            }

            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("projectFingerprint must not be empty.", nameof(projectFingerprint));
            }

            services.AddUnityRuntimeServices(DaemonEditorMode.Gui);
            services.AddSingleton<ISessionTokenValidator>(sessionTokenValidator);
            services.AddSingleton<IDaemonLogger>(daemonLogger);
            services.AddSingleton<IUnityGuiBootstrapStarter, UnityGuiBootstrapStarter>();
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider => new GuiRebootstrapUnityIpcMethodHandler(
                bootstrapStarter: serviceProvider.GetRequiredService<IUnityGuiBootstrapStarter>(),
                projectFingerprint: projectFingerprint,
                daemonLogger: daemonLogger));
            services.AddSingleton<IUnityIpcMethodDispatcher>(serviceProvider => CreateMethodDispatcher(
                serviceProvider,
                recoverableOperationStore: null));
            services.AddSingleton<IUnityIpcRequestHandler, UnityIpcRequestHandler>();
            services.AddSingleton<IDaemonShutdownSignal, DaemonShutdownSignal>();
            services.AddSingleton<IUnityShutdownAdmissionCoordinator, UnityShutdownAdmissionCoordinator>();
            services.AddSingleton<IUnityIpcConnectionHandler>(CreateConnectionHandler);
            AddTransportListeners(services);
            services.AddSingleton<IUnityIpcServer>(serviceProvider =>
            {
                return new UnityIpcServer(
                    serviceProvider.GetRequiredService<IUnityIpcConnectionHandler>(),
                    new IUnityIpcTransportListener[]
                    {
                        serviceProvider.GetRequiredService<NamedPipeUnityIpcTransportListener>(),
                        serviceProvider.GetRequiredService<UnixDomainSocketUnityIpcTransportListener>(),
                    },
                    serviceProvider.GetRequiredService<IDaemonShutdownSignal>(),
                    serviceProvider.GetRequiredService<IDaemonLogger>(),
                    UnityIpcServer.DefaultListenerStopTimeout);
            });
            return services;
        }

        private static UnityIpcConnectionHandler CreateConnectionHandler (IServiceProvider serviceProvider)
        {
            return new UnityIpcConnectionHandler(
                serviceProvider.GetRequiredService<IUnityIpcRequestHandler>(),
                serviceProvider.GetRequiredService<IUnityShutdownAdmissionCoordinator>(),
                UnityIpcConnectionHandler.DefaultInitialFrameReadTimeout,
                UnityIpcConnectionHandler.DefaultResponseFrameWriteTimeout);
        }

        private static UnityIpcMethodDispatcher CreateMethodDispatcher (
            IServiceProvider serviceProvider,
            IRecoverableIpcOperationStore recoverableOperationStore)
        {
            return new UnityIpcMethodDispatcher(
                serviceProvider.GetServices<IUnityIpcMethodHandler>(),
                serviceProvider.GetRequiredService<IUnityMainThreadRequestExecutor>(),
                serviceProvider.GetRequiredService<IUnityControlPlaneRequestExecutor>(),
                recoverableOperationStore,
                serviceProvider.GetRequiredService<IDaemonLogger>());
        }

        private static void AddTransportListeners (IServiceCollection services)
        {
            services.AddSingleton(serviceProvider => new NamedPipeUnityIpcTransportListener(
                serviceProvider.GetRequiredService<IDaemonLogger>(),
                MaximumActiveTransportConnections,
                ConnectionDrainTimeout));
            services.AddSingleton(serviceProvider => new UnixDomainSocketUnityIpcTransportListener(
                serviceProvider.GetRequiredService<IDaemonLogger>(),
                MaximumActiveTransportConnections,
                ConnectionDrainTimeout));
        }
    }
}
