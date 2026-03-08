using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;

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
            services
                .AddUnityIpcApplicationServices(
                    new FileBackedSessionTokenValidator(bootstrapArguments.SessionPath),
                    daemonLogger)
                .AddUnityIpcDaemonHostServices(
                    bootstrapArguments,
                    daemonLogStream);

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
    }
}
