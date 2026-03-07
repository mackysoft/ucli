using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Bootstraps IPC daemon server when Unity is launched in batchmode daemon mode. </summary>
    internal static class UnityDaemonBootstrap
    {
        /// <summary> Starts Unity daemon mode after batchmode initialization is ready. </summary>
        /// <returns> A task that completes after daemon mode exits or bootstrap failure requests process exit. </returns>
        internal static Task Start (IpcDaemonBootstrapArguments bootstrapArguments)
        {
            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            var daemonLogStream = new DaemonLogRingBuffer();
            var daemonLogger = new DaemonLogger(daemonLogStream);

            return RunSafely(bootstrapArguments, daemonLogStream, daemonLogger);
        }

        private static async Task RunSafely (
            IpcDaemonBootstrapArguments bootstrapArguments,
            IDaemonLogStream daemonLogStream,
            IDaemonLogger daemonLogger)
        {
            try
            {
                await Run(bootstrapArguments, daemonLogStream, daemonLogger);
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "uCLI daemon bootstrap failed with unhandled exception.",
                    exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary> Starts IPC server and blocks until shutdown request is received. </summary>
        /// <returns> A task that completes after process-exit request has been issued. </returns>
        private static async Task Run (
            IpcDaemonBootstrapArguments bootstrapArguments,
            IDaemonLogStream daemonLogStream,
            IDaemonLogger daemonLogger)
        {
            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            if (daemonLogStream == null)
            {
                throw new ArgumentNullException(nameof(daemonLogStream));
            }

            if (daemonLogger == null)
            {
                throw new ArgumentNullException(nameof(daemonLogger));
            }

            if (!IpcTransportKindCodec.TryParse(bootstrapArguments.EndpointTransportKind, out var transportKind))
            {
                daemonLogger.Error(
                    DaemonLogCategories.Lifecycle,
                    $"Unsupported endpoint transport kind: {bootstrapArguments.EndpointTransportKind}");
                EditorApplication.Exit(1);
                return;
            }

            var services = new ServiceCollection();
            services.AddSingleton(static _ => UcliOperationCatalogSnapshotBuilder.Build());
            services.AddSingleton(bootstrapArguments);
            services.AddSingleton<IDaemonLogStream>(daemonLogStream);
            services.AddSingleton<IDaemonLogger>(daemonLogger);
            services.AddSingleton<IUnityLogStream, UnityLogRingBuffer>();
            services.AddSingleton<UnityCompileMessageDedupeCache>();
            services.AddSingleton<UnityLogCollector>();
            services.AddSingleton<UnityLogCaptureService>();
            services.AddSingleton<IUnityMainThreadRequestExecutor>(
                new UnitySynchronizationContextRequestExecutor());
            services.AddSingleton<IDaemonShutdownSignal, DaemonShutdownSignal>();
            services.AddSingleton<ISessionTokenValidator>(new FileBackedSessionTokenValidator(bootstrapArguments.SessionPath));
            services.AddSingleton<IExecuteRequestDispatcher>(serviceProvider => CreateExecuteRequestDispatcher(
                serviceProvider.GetRequiredService<UcliOperationCatalogSnapshot>()));
            services.AddSingleton<IEditorLogRangeExporter, EditorLogRangeExporter>();
            services.AddSingleton<IUnityTestRunRequestContextFactory, UnityTestRunRequestContextFactory>();
            services.AddSingleton<IUnityTestRunner, UnityTestRunner>();
            services.AddSingleton<IUnityTestResultsXmlWriter, UnityTestResultsXmlWriter>();
            services.AddSingleton<IUnityTestRunService, UnityTestRunService>();
            services.AddSingleton<IServerVersionProvider, AssemblyServerVersionProvider>();
            services.AddSingleton<IDaemonLogsReadRequestValidator, DaemonLogsReadRequestValidator>();
            services.AddSingleton<IDaemonLogsReadQueryEngine, DaemonLogsReadQueryEngine>();
            services.AddSingleton<DaemonLogsReadResponseFactory>();
            services.AddSingleton<UnityLogsReadRequestValidator>();
            services.AddSingleton<UnityLogsReadQueryEngine>();
            services.AddSingleton<UnityLogsReadResponseFactory>();
            services.AddSingleton<IUnityIpcMethodHandler, PingUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, ExecuteUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, TestRunUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, OpsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, DaemonLogsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, UnityLogsReadUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodHandler, ShutdownUnityIpcMethodHandler>();
            services.AddSingleton<IUnityIpcMethodDispatcher, UnityIpcMethodDispatcher>();
            services.AddSingleton<IUnityIpcRequestHandler, UnityIpcRequestHandler>();
            services.AddSingleton<IUnityIpcConnectionHandler, UnityIpcConnectionHandler>();
            services.AddSingleton<NamedPipeUnityIpcTransportListener>();
            services.AddSingleton<UnixDomainSocketUnityIpcTransportListener>();
            services.AddSingleton<IUnityIpcServer>(serviceProvider =>
            {
                return new UnityIpcServer(
                    serviceProvider.GetRequiredService<IUnityIpcRequestHandler>(),
                    serviceProvider.GetRequiredService<IUnityIpcConnectionHandler>(),
                    new IUnityIpcTransportListener[]
                    {
                        serviceProvider.GetRequiredService<NamedPipeUnityIpcTransportListener>(),
                        serviceProvider.GetRequiredService<UnixDomainSocketUnityIpcTransportListener>(),
                    },
                    serviceProvider.GetRequiredService<IDaemonLogger>());
            });

            using var serviceProvider = services.BuildServiceProvider();
            var server = serviceProvider.GetRequiredService<IUnityIpcServer>();
            var shutdownSignal = serviceProvider.GetRequiredService<IDaemonShutdownSignal>();
            using var unityLogCaptureService = serviceProvider.GetRequiredService<UnityLogCaptureService>();
            unityLogCaptureService.Start();

            var endpoint = new IpcEndpoint(transportKind, bootstrapArguments.EndpointAddress);
            await server.Start(endpoint, CancellationToken.None);
            daemonLogger.Info(
                DaemonLogCategories.Lifecycle,
                $"uCLI daemon started. repoRoot={bootstrapArguments.RepositoryRoot}, fingerprint={bootstrapArguments.ProjectFingerprint}, endpoint={bootstrapArguments.EndpointAddress}");

            var shutdownWaitTask = shutdownSignal.Wait(CancellationToken.None);
            var serverTerminationTask = server.WaitForTermination(CancellationToken.None);
            var completedTask = await Task.WhenAny(shutdownWaitTask, serverTerminationTask);
            if (ReferenceEquals(completedTask, serverTerminationTask))
            {
                await serverTerminationTask;
                daemonLogger.Error(
                    DaemonLogCategories.Lifecycle,
                    "IPC server loop terminated before daemon shutdown signal.");
                throw new InvalidOperationException("IPC server loop terminated before shutdown request was received.");
            }

            await shutdownWaitTask;
            daemonLogger.Info(
                DaemonLogCategories.Lifecycle,
                "Daemon shutdown signal received. Stopping IPC server.");
            await server.Stop(CancellationToken.None);
            daemonLogger.Info(
                DaemonLogCategories.Lifecycle,
                "IPC server stop completed. Exiting Unity batchmode process.");
            EditorApplication.Exit(0);
        }

        /// <summary> Creates execute-request dispatcher used by Unity daemon mode. </summary>
        /// <returns> The execute-request dispatcher instance. </returns>
        private static IExecuteRequestDispatcher CreateExecuteRequestDispatcher (UcliOperationCatalogSnapshot snapshot)
        {
            var normalizer = new ExecuteRequestNormalizer();
            var operationRegistry = new InMemoryPhaseOperationRegistry(snapshot.Registrations);
            var phaseExecutor = new OperationPhaseExecutor(operationRegistry);
            return new ExecuteRequestDispatcher(normalizer, phaseExecutor);
        }
    }
}
