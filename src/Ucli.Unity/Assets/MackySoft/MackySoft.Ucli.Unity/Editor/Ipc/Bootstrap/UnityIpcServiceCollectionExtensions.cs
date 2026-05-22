using System;
using System.IO;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
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
            DaemonEditorMode editorMode = DaemonEditorMode.Batchmode)
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
            services.AddSingleton<IEditorLogRangeExporter, EditorLogRangeExporter>();
            services.AddSingleton<IUnityTestRunRequestContextFactory, UnityTestRunRequestContextFactory>();
            services.AddSingleton<IUnityTestRunner, UnityTestRunner>();
            services.AddSingleton<IUnityTestResultsXmlWriter, UnityTestResultsXmlWriter>();
            services.AddSingleton<IUnityTestRunService, UnityTestRunService>();
            services.AddSingleton<IServerVersionProvider, AssemblyServerVersionProvider>();
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
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider =>
            {
                return new PlayStatusUnityIpcMethodHandler(
                    serviceProvider.GetRequiredService<IServerVersionProvider>(),
                    serviceProvider.GetRequiredService<IUnityEditorReadinessGate>(),
                    serviceProvider.GetRequiredService<IpcProjectIdentity>(),
                    serviceProvider.GetRequiredService<IDaemonLogger>());
            });
            services.AddSingleton<IUnityIpcMethodHandler>(serviceProvider =>
            {
                return new PlayEnterUnityIpcMethodHandler(
                    serviceProvider.GetRequiredService<IServerVersionProvider>(),
                    serviceProvider.GetRequiredService<IUnityEditorReadinessGate>(),
                    serviceProvider.GetRequiredService<IpcProjectIdentity>(),
                    serviceProvider.GetRequiredService<IDaemonLogger>());
            });
            services.AddSingleton<IUnityIpcMethodHandler, TestRunUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, OpsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, IndexAssetsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, IndexSceneTreeLiteReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodDispatcher, UnityIpcMethodDispatcher>();
            services.AddSingleton<IUnityIpcRequestHandler, UnityIpcRequestHandler>();
            services.AddSingleton<IUnityIpcRequestProcessor, UnityIpcRequestProcessor>();
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
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityIpcDaemonHostServices (
            this IServiceCollection services,
            IpcDaemonBootstrapArguments bootstrapArguments,
            IDaemonLogStream daemonLogStream)
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

            services.AddSingleton(bootstrapArguments);
            services.AddSingleton<IDaemonLogStream>(daemonLogStream);
            services.AddSingleton<IUnityLogStream, UnityLogRingBuffer>();
            services.AddSingleton<UnityCompileMessageDedupeCache>();
            services.AddSingleton<UnityLogCollector>();
            services.AddSingleton<UnityLogCaptureService>();
            services.AddSingleton<IDaemonShutdownSignal, DaemonShutdownSignal>();
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
            services.AddSingleton<IUnityIpcConnectionHandler, UnityIpcConnectionHandler>();
            services.AddSingleton<NamedPipeUnityIpcTransportListener>();
            services.AddSingleton<UnixDomainSocketUnityIpcTransportListener>();
            services.AddSingleton<IUnityIpcServer>(serviceProvider =>
            {
                return new UnityIpcServer(
                    serviceProvider.GetRequiredService<IUnityIpcRequestProcessor>(),
                    serviceProvider.GetRequiredService<IUnityIpcConnectionHandler>(),
                    new IUnityIpcTransportListener[]
                    {
                        serviceProvider.GetRequiredService<NamedPipeUnityIpcTransportListener>(),
                        serviceProvider.GetRequiredService<UnixDomainSocketUnityIpcTransportListener>(),
                    },
                    serviceProvider.GetRequiredService<IDaemonLogger>());
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
            services.AddSingleton<OneshotRequestCompletionSignal>();
            services.AddSingleton<IUnityIpcMethodHandler, ShutdownUnityIpcMethodHandler>();
            services.AddSingleton<UnityIpcConnectionHandler>();
            services.AddSingleton<IUnityIpcConnectionHandler, UnityOneshotConnectionHandler>();
            services.AddSingleton<NamedPipeUnityIpcTransportListener>();
            services.AddSingleton<UnixDomainSocketUnityIpcTransportListener>();
            services.AddSingleton<IUnityIpcServer>(serviceProvider =>
            {
                return new UnityIpcServer(
                    serviceProvider.GetRequiredService<IUnityIpcRequestProcessor>(),
                    serviceProvider.GetRequiredService<IUnityIpcConnectionHandler>(),
                    new IUnityIpcTransportListener[]
                    {
                        serviceProvider.GetRequiredService<NamedPipeUnityIpcTransportListener>(),
                        serviceProvider.GetRequiredService<UnixDomainSocketUnityIpcTransportListener>(),
                    },
                    serviceProvider.GetRequiredService<IDaemonLogger>());
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
            services.AddSingleton<IUnityIpcMethodHandler>(_ => new GuiRebootstrapUnityIpcMethodHandler(projectFingerprint, daemonLogger));
            services.AddSingleton<IUnityIpcMethodDispatcher, UnityIpcMethodDispatcher>();
            services.AddSingleton<IUnityIpcRequestHandler, UnityIpcRequestHandler>();
            services.AddSingleton<IUnityIpcRequestProcessor, UnityIpcRequestProcessor>();
            services.AddSingleton<IDaemonShutdownSignal, DaemonShutdownSignal>();
            services.AddSingleton<IUnityIpcConnectionHandler, UnityIpcConnectionHandler>();
            services.AddSingleton<NamedPipeUnityIpcTransportListener>();
            services.AddSingleton<UnixDomainSocketUnityIpcTransportListener>();
            services.AddSingleton<IUnityIpcServer>(serviceProvider =>
            {
                return new UnityIpcServer(
                    serviceProvider.GetRequiredService<IUnityIpcRequestProcessor>(),
                    serviceProvider.GetRequiredService<IUnityIpcConnectionHandler>(),
                    new IUnityIpcTransportListener[]
                    {
                        serviceProvider.GetRequiredService<NamedPipeUnityIpcTransportListener>(),
                        serviceProvider.GetRequiredService<UnixDomainSocketUnityIpcTransportListener>(),
                    },
                    serviceProvider.GetRequiredService<IDaemonLogger>());
            });
            return services;
        }
    }
}
