using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Index;
using MackySoft.Ucli.Unity.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Registers shared Unity IPC application services used by daemon and oneshot hosts. </summary>
    internal static class UnityIpcServiceCollectionExtensions
    {
        /// <summary> Registers shared IPC application services and method handlers. </summary>
        /// <param name="services"> The target service collection. </param>
        /// <param name="sessionTokenValidator"> The session-token validator used by the host. </param>
        /// <param name="daemonLogger"> The daemon logger used by the host. </param>
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityIpcApplicationServices (
            this IServiceCollection services,
            ISessionTokenValidator sessionTokenValidator,
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

            // NOTE:
            // Project owner exposes static helpers only, so service composition starts from Runtime.
            services.AddUnityRuntimeServices();
            services.AddUnityIndexServices();
            services.AddUnityExecutionServices();
            services.AddSingleton<ISessionTokenValidator>(sessionTokenValidator);
            services.AddSingleton<IDaemonLogger>(daemonLogger);
            services.AddSingleton<IEditorLogRangeExporter, EditorLogRangeExporter>();
            services.AddSingleton<IUnityTestRunRequestContextFactory, UnityTestRunRequestContextFactory>();
            services.AddSingleton<IUnityTestRunner, UnityTestRunner>();
            services.AddSingleton<IUnityTestResultsXmlWriter, UnityTestResultsXmlWriter>();
            services.AddSingleton<IUnityTestRunService, UnityTestRunService>();
            services.AddSingleton<IServerVersionProvider, AssemblyServerVersionProvider>();
            services.AddSingleton<IUnityIpcMethodHandler, PingUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, ExecuteUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, TestRunUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, OpsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, IndexAssetsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, IndexSceneTreeLiteReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodDispatcher, UnityIpcMethodDispatcher>();
            services.AddSingleton<IUnityIpcRequestHandler, UnityIpcRequestHandler>();
            services.AddSingleton<IUnityIpcRequestProcessor, UnityIpcRequestProcessor>();
            return services;
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
            services.AddSingleton<IUnityIpcMethodHandler, DaemonLogsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, UnityLogsReadUnityIpcMethodHandler>();
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
    }
}
