using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Execution.ReadPostcondition;
using MackySoft.Ucli.Unity.Build;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Execution.Program;
using MackySoft.Ucli.Unity.Index;
using MackySoft.Ucli.Unity.Project;
using MackySoft.Ucli.Unity.Recording;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Unity.ScreenshotCapture.Capture;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution;
using MackySoft.Ucli.Unity.ScreenshotCapture.SceneView;
using MackySoft.Ucli.Unity.ScreenshotCapture.Staging;
using Microsoft.Extensions.DependencyInjection;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

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
            ProjectFingerprint projectFingerprint,
            IDaemonLogger daemonLogger,
            UnityEditorMode editorMode)
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

            if (projectFingerprint == null)
            {
                throw new ArgumentNullException(nameof(projectFingerprint));
            }

            // NOTE:
            // Project owner exposes static helpers only, so service composition starts from Runtime.
            services.AddUnityRuntimeServices(editorMode);
            services.AddUnityIndexServices();
            services.AddUnityExecutionServices();
            var projectIdentity = UnityProjectIdentityFactory.Create(projectFingerprint);
            services.AddSingleton(projectIdentity);
            services.AddSingleton(projectIdentity.IpcIdentity);
            services.AddSingleton<IUnityProgramEffectiveConfigurationSource, UnityProgramEffectiveConfigurationSource>();
            services.AddSingleton<ISessionTokenValidator>(sessionTokenValidator);
            services.AddSingleton<IDaemonLogger>(daemonLogger);
            services.AddSingleton<UnityLogRedactionScopeProvider>();
            services.AddSingleton<IUnityLogStream, UnityLogRingBuffer>();
            services.AddSingleton<IEditorLogRangeExporter, EditorLogRangeExporter>();
            services.AddSingleton<IUnityTestRunRequestContextFactory, UnityTestRunRequestContextFactory>();
            services.AddSingleton<IUnityTestRunner, UnityTestRunner>();
            services.AddSingleton<IUnityTestResultsXmlWriter, UnityTestResultsXmlWriter>();
            services.AddSingleton<IUnityTestRunService, UnityTestRunService>();
            services.AddSingleton<IIpcRequestPhaseScopeFactory, IpcRequestPhaseScopeFactory>();
            services.AddSingleton<IServerVersionProvider, AssemblyServerVersionProvider>();
            services.AddSingleton<IUnityEditorUpdateAwaiter, UnityEditorUpdateAwaiterAdapter>();
            services.AddSingleton<IUnityPlayModeController, UnityEditorPlayModeController>();
            services.AddSingleton<IUnityAssetRefreshController, UnityAssetRefreshController>();
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
            services.AddSingleton<ILifecycleExecutionTimeSource, SystemLifecycleExecutionTimeSource>();
            services.AddSingleton<FileRefreshLifecycleExecutionCheckpointStore>();
            services.AddSingleton<FileCompileLifecycleExecutionCheckpointStore>();
            services.AddSingleton<FilePlayEnterLifecycleExecutionCheckpointStore>();
            services.AddSingleton<FilePlayExitLifecycleExecutionCheckpointStore>();
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider =>
            {
                return new PingUnityIpcMethodHandler(
                    serviceProvider.GetRequiredService<IServerVersionProvider>(),
                    serviceProvider.GetRequiredService<IUnityEditorAvailabilityObservationSource>(),
                    serviceProvider.GetRequiredService<UnityProjectIdentity>(),
                    serviceProvider.GetRequiredService<IDaemonLogger>());
            });
            services.AddSingleton<IUnityIpcMethodHandler, ProgramExecutionContextUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, ProgramRequestExecutionUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, ProgramRequestAttachUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, ProgramRequestCancelUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, ExecuteUnityIpcMethodHandler>();
            services.AddSingleton<MutationReadPostconditionJournal>();
            services.AddSingleton<IUnityIpcMethodHandler, EvalPlanUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, EvalCallUnityIpcMethodHandler>();
            services.AddSingleton<
                ILifecycleExecutionStartAdmissionPolicy,
                RefreshLifecycleExecutionStartAdmissionPolicy>();
            services.AddSingleton<IUnityIpcMethodHandler, LifecycleExecutionStartUnityIpcMethodHandler>();
            services.AddSingleton<
                IRefreshLifecycleExecutionProvider,
                UnityEditorRefreshLifecycleExecutionProvider>();
            services.AddSingleton<RefreshLifecycleExecutionHandler>();
            services.AddSingleton<IRefreshLifecycleExecutionHandler>(serviceProvider =>
                serviceProvider.GetRequiredService<RefreshLifecycleExecutionHandler>());
            services.AddSingleton<RefreshUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider =>
                serviceProvider.GetRequiredService<RefreshUnityIpcMethodHandler>());
            services.AddSingleton<ILifecycleExecutionRecoveryHandler>(serviceProvider =>
                serviceProvider.GetRequiredService<RefreshLifecycleExecutionHandler>());
            services.AddSingleton<
                ICompileLifecycleExecutionProvider,
                UnityEditorCompileLifecycleExecutionProvider>();
            services.AddSingleton<CompileLifecycleExecutionHandler>();
            services.AddSingleton<ICompileLifecycleExecutionHandler>(serviceProvider =>
                serviceProvider.GetRequiredService<CompileLifecycleExecutionHandler>());
            services.AddSingleton<CompileUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider =>
                serviceProvider.GetRequiredService<CompileUnityIpcMethodHandler>());
            services.AddSingleton<ILifecycleExecutionRecoveryHandler>(serviceProvider =>
                serviceProvider.GetRequiredService<CompileLifecycleExecutionHandler>());
            services.AddSingleton<IUnityIpcMethodHandler, BuildRunUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider =>
            {
                return new PlayStatusUnityIpcMethodHandler(
                    serviceProvider.GetRequiredService<IServerVersionProvider>(),
                    serviceProvider.GetRequiredService<IUnityEditorAvailabilityObservationSource>(),
                    serviceProvider.GetRequiredService<UnityProjectIdentity>(),
                    serviceProvider.GetRequiredService<IDaemonLogger>());
            });
            services.AddSingleton<
                IPlayEnterLifecycleExecutionProvider,
                UnityEditorPlayEnterLifecycleExecutionProvider>();
            services.AddSingleton<PlayEnterLifecycleExecutionHandler>();
            services.AddSingleton<IPlayEnterLifecycleExecutionHandler>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayEnterLifecycleExecutionHandler>());
            services.AddSingleton<PlayEnterUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayEnterUnityIpcMethodHandler>());
            services.AddSingleton<ILifecycleExecutionRecoveryHandler>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayEnterLifecycleExecutionHandler>());
            services.AddSingleton<
                IPlayExitLifecycleExecutionProvider,
                UnityEditorPlayExitLifecycleExecutionProvider>();
            services.AddSingleton<PlayExitLifecycleExecutionHandler>();
            services.AddSingleton<IPlayExitLifecycleExecutionHandler>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayExitLifecycleExecutionHandler>());
            services.AddSingleton<PlayExitUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayExitUnityIpcMethodHandler>());
            services.AddSingleton<ILifecycleExecutionRecoveryHandler>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayExitLifecycleExecutionHandler>());
            services.AddSingleton<IUnityIpcMethodHandler, TestRunUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, OpsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, IndexAssetsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, IndexSceneTreeLiteReadUnityIpcMethodHandler>();
            return services;
        }

        /// <summary> Registers daemon-only transport, logging, and lifetime services. </summary>
        /// <param name="services"> The target service collection. </param>
        /// <param name="bootstrapContext"> The guarded daemon bootstrap context. </param>
        /// <param name="daemonLogStream"> The daemon log stream. </param>
        /// <param name="editorInstanceId"> The non-empty Editor process identity captured for this host generation. </param>
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityIpcDaemonHostServices (
            this IServiceCollection services,
            UnityDaemonBootstrapContext bootstrapContext,
            IDaemonLogStream daemonLogStream,
            Guid editorInstanceId,
            DaemonLifecycleRecoveryLease recoveryLease = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (bootstrapContext == null)
            {
                throw new ArgumentNullException(nameof(bootstrapContext));
            }

            if (daemonLogStream == null)
            {
                throw new ArgumentNullException(nameof(daemonLogStream));
            }

            if (editorInstanceId == Guid.Empty)
            {
                throw new ArgumentException("Editor instance identifier must not be empty.", nameof(editorInstanceId));
            }

            services.AddSingleton(bootstrapContext);
            services.AddSingleton<IDaemonLogStream>(daemonLogStream);
            AddLifecycleExecutionHostServices(
                services,
                bootstrapContext.SessionGenerationId,
                editorInstanceId,
                recoveryLease);
            services.AddSingleton<ILifecycleExecutionHostLifetimeObserver>(
                NoOpLifecycleExecutionHostLifetimeObserver.Instance);
            services.AddSingleton<IUnityIpcMethodDispatcher>(CreateMethodDispatcher);
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
            services.AddSingleton<IUnityScreenshotResolutionOrphanCleaner, UnityScreenshotResolutionOrphanCleaner>();
            services.AddSingleton<UnityGameViewResolutionAdapter>();
            services.AddSingleton<IGameViewPresentationAdapter, UnityGameViewPresentationAdapter>();
            services.AddSingleton<UnityGameViewScreenshotCapture>();
            services.AddSingleton<UnitySceneViewPresentationAdapter>();
            services.AddSingleton<UnitySceneViewScreenshotCapture>();
            services.AddSingleton<IUnityScreenshotCaptureBackend, UnityEditorScreenshotCaptureBackend>();
            services.AddSingleton<IScreenshotStagingImageWriter, ScreenshotStagingImageWriter>();
            services.AddSingleton<IUnityScreenshotCaptureService, UnityScreenshotCaptureService>();
            services.AddSingleton(GameViewRecordingAdapterRegistry.Shared);
            services.AddSingleton<GameViewRecordingIpcProjection>();
            services.AddSingleton<IGameViewRecorderPackageRegistry, UnityEditorGameViewRecorderPackageRegistry>();
            services.AddSingleton<IUnityIpcMethodHandler, GameViewRecordingCapabilityUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, GameViewRecordingStartUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, GameViewRecordingStatusUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, GameViewRecordingStopUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, DaemonLogsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, UnityLogsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, UnityConsoleClearUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, ScreenshotCaptureUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, ShutdownUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcConnectionHandler>(CreateConnectionHandler);
            AddTransportListeners(services, bootstrapContext.EndpointBinding);
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
        /// <param name="endpointBinding"> The guarded runtime binding derived and validated for this oneshot host. </param>
        /// <param name="lifetimeWatchdog"> The watchdog instance that owns this oneshot process lifetime. </param>
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityIpcOneshotHostServices (
            this IServiceCollection services,
            UnityIpcEndpointBinding endpointBinding,
            OneshotProcessLifetimeWatchdog lifetimeWatchdog,
            Guid endpointRegistrationGenerationId,
            Guid editorInstanceId)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (endpointBinding == null)
            {
                throw new ArgumentNullException(nameof(endpointBinding));
            }

            if (lifetimeWatchdog == null)
            {
                throw new ArgumentNullException(nameof(lifetimeWatchdog));
            }

            AddLifecycleExecutionHostServices(
                services,
                endpointRegistrationGenerationId,
                editorInstanceId,
                recoveryLease: null);
            services.AddSingleton<IDaemonShutdownSignal, DaemonShutdownSignal>();
            services.AddSingleton<IUnityShutdownAdmissionCoordinator, UnityShutdownAdmissionCoordinator>();
            services.AddSingleton<IUnityIpcMethodDispatcher>(CreateMethodDispatcher);
            services.AddSingleton<IUnityIpcRequestHandler, UnityIpcRequestHandler>();
            services.AddSingleton(lifetimeWatchdog);
            services.AddSingleton<ILifecycleExecutionHostLifetimeObserver>(
                lifetimeWatchdog);
            services.AddSingleton<OneshotRequestCompletionSignal>();
            services.AddSingleton<ILifecycleExecutionTerminalObserver>(
                serviceProvider => serviceProvider
                    .GetRequiredService<OneshotRequestCompletionSignal>());
            services.AddSingleton<IUnityIpcMethodHandler, ShutdownUnityIpcMethodHandler>();
            services.AddSingleton(CreateConnectionHandler);
            services.AddSingleton<IUnityIpcConnectionHandler, UnityOneshotConnectionHandler>();
            AddTransportListeners(services, endpointBinding);
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
        /// <param name="endpointBinding"> The guarded runtime binding derived for this GUI-supervisor host. </param>
        /// <param name="daemonLogger"> The daemon logger used by the host. </param>
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityGuiSupervisorHostServices (
            this IServiceCollection services,
            ISessionTokenValidator sessionTokenValidator,
            ProjectFingerprint projectFingerprint,
            UnityIpcEndpointBinding endpointBinding,
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

            if (projectFingerprint == null)
            {
                throw new ArgumentNullException(nameof(projectFingerprint));
            }

            if (endpointBinding == null)
            {
                throw new ArgumentNullException(nameof(endpointBinding));
            }

            services.AddUnityRuntimeServices(UnityEditorMode.Gui);
            services.AddSingleton<ISessionTokenValidator>(sessionTokenValidator);
            services.AddSingleton<IDaemonLogger>(daemonLogger);
            services.AddSingleton<IIpcRequestPhaseScopeFactory, IpcRequestPhaseScopeFactory>();
            services.AddSingleton<IUnityGuiBootstrapStarter, UnityGuiBootstrapStarter>();
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider => new GuiRebootstrapUnityIpcMethodHandler(
                bootstrapStarter: serviceProvider.GetRequiredService<IUnityGuiBootstrapStarter>(),
                projectFingerprint: projectFingerprint,
                daemonLogger: daemonLogger));
            services.AddSingleton<IUnityIpcMethodDispatcher>(CreateMethodDispatcher);
            services.AddSingleton<IUnityIpcRequestHandler, UnityIpcRequestHandler>();
            services.AddSingleton<IDaemonShutdownSignal, DaemonShutdownSignal>();
            services.AddSingleton<IUnityShutdownAdmissionCoordinator, UnityShutdownAdmissionCoordinator>();
            services.AddSingleton<IUnityIpcConnectionHandler>(CreateConnectionHandler);
            AddTransportListeners(services, endpointBinding);
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
                requestHandler: serviceProvider.GetRequiredService<IUnityIpcRequestHandler>(),
                shutdownAdmissionCoordinator: serviceProvider.GetRequiredService<IUnityShutdownAdmissionCoordinator>(),
                phaseScopeFactory: serviceProvider.GetRequiredService<IIpcRequestPhaseScopeFactory>(),
                initialFrameReadTimeout: UnityIpcConnectionHandler.DefaultInitialFrameReadTimeout,
                responseFrameWriteTimeout: UnityIpcConnectionHandler.DefaultResponseFrameWriteTimeout);
        }

        private static UnityIpcMethodDispatcher CreateMethodDispatcher (IServiceProvider serviceProvider)
        {
            return new UnityIpcMethodDispatcher(
                serviceProvider.GetServices<IUnityIpcMethodHandler>(),
                serviceProvider.GetRequiredService<IUnityMainThreadRequestExecutor>(),
                serviceProvider.GetRequiredService<IUnityControlPlaneRequestExecutor>());
        }

        private static void AddTransportListeners (
            IServiceCollection services,
            UnityIpcEndpointBinding endpointBinding)
        {
            services.AddSingleton(endpointBinding);
            services.AddSingleton(serviceProvider => new NamedPipeUnityIpcTransportListener(
                serviceProvider.GetRequiredService<IDaemonLogger>(),
                MaximumActiveTransportConnections,
                ConnectionDrainTimeout));
            services.AddSingleton(serviceProvider => new UnixDomainSocketUnityIpcTransportListener(
                serviceProvider.GetRequiredService<IDaemonLogger>(),
                serviceProvider.GetRequiredService<UnityIpcEndpointBinding>(),
                MaximumActiveTransportConnections,
                ConnectionDrainTimeout));
        }

        private static void AddLifecycleExecutionHostServices (
            IServiceCollection services,
            Guid endpointRegistrationGenerationId,
            Guid editorInstanceId,
            DaemonLifecycleRecoveryLease recoveryLease)
        {
            if (endpointRegistrationGenerationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Endpoint registration generation identifier must not be empty.",
                    nameof(endpointRegistrationGenerationId));
            }

            if (editorInstanceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Editor instance identifier must not be empty.",
                    nameof(editorInstanceId));
            }

            services.AddSingleton(new UnityLifecycleExecutionHostContext(
                ProcessLivenessProbe.CaptureCurrentProcess(),
                editorInstanceId,
                endpointRegistrationGenerationId,
                recoveryLease));
            services.AddSingleton<ILifecycleExecutionTerminalObserver>(
                NoOpLifecycleExecutionTerminalObserver.Instance);
            services.AddSingleton(serviceProvider =>
            {
                var project = serviceProvider.GetRequiredService<UnityHostProjectIdentity>();
                return FileLifecycleExecutionStore.CreateForProject(
                    project.ProjectPath,
                    project.ProjectFingerprint);
            });
            services.AddSingleton<UnityLifecycleExecutionRecoveryCoordinator>();
            services.AddSingleton<ILifecycleExecutionDeadlineScheduler>(
                serviceProvider => serviceProvider
                    .GetRequiredService<UnityLifecycleExecutionRecoveryCoordinator>());
        }
    }
}
