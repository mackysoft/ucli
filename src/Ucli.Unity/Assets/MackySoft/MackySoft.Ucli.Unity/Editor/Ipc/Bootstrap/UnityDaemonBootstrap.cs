using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Bootstraps IPC daemon server when Unity is launched in batchmode daemon mode. </summary>
    internal static class UnityDaemonBootstrap
    {
        /// <summary> Starts Unity daemon mode after batchmode initialization is ready. </summary>
        /// <returns> A task that completes after daemon mode exits or bootstrap failure requests process exit. </returns>
        internal static async Task Start (IpcDaemonBootstrapArguments bootstrapArguments)
        {
            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            var daemonLogStream = new DaemonLogRingBuffer();
            var daemonLogger = new DaemonLogger(daemonLogStream);
            var daemonStarted = false;
            var diagnosisWritten = false;

            try
            {
                if (!IpcTransportKindCodec.TryParse(bootstrapArguments.EndpointTransportKind, out var transportKind))
                {
                    var errorMessage = $"Unsupported endpoint transport kind: {bootstrapArguments.EndpointTransportKind}";
                    daemonLogger.Error(
                        DaemonLogCategories.Lifecycle,
                        errorMessage);
                    diagnosisWritten = await PersistDiagnosis(
                        bootstrapArguments,
                        DaemonDiagnosisReasonValues.StartupFailed,
                        errorMessage,
                        daemonLogger);
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

                IServiceProvider serviceProvider = services.BuildServiceProvider();
                try
                {
                    var server = serviceProvider.GetRequiredService<IUnityIpcServer>();
                    var shutdownSignal = serviceProvider.GetRequiredService<IDaemonShutdownSignal>();
                    using var unityLogCaptureService = serviceProvider.GetRequiredService<UnityLogCaptureService>();
                    unityLogCaptureService.Start();

                    var endpoint = new IpcEndpoint(transportKind, bootstrapArguments.EndpointAddress);
                    await server.Start(endpoint, CancellationToken.None);
                    daemonStarted = true;
                    daemonLogger.Info(
                        DaemonLogCategories.Lifecycle,
                        $"uCLI daemon started. repoRoot={bootstrapArguments.RepositoryRoot}, fingerprint={bootstrapArguments.ProjectFingerprint}, endpoint={bootstrapArguments.EndpointAddress}");

                    var shutdownWaitTask = shutdownSignal.Wait(CancellationToken.None);
                    var serverTerminationTask = server.WaitForTermination(CancellationToken.None);
                    var completedTask = await Task.WhenAny(shutdownWaitTask, serverTerminationTask);
                    if (ReferenceEquals(completedTask, serverTerminationTask))
                    {
                        await serverTerminationTask;
                        const string Message = "IPC server loop terminated before daemon shutdown signal.";
                        daemonLogger.Error(
                            DaemonLogCategories.Lifecycle,
                            Message);
                        diagnosisWritten = await PersistDiagnosis(
                            bootstrapArguments,
                            DaemonDiagnosisReasonValues.ListenerTerminated,
                            Message,
                            daemonLogger);
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
                    diagnosisWritten = await PersistDiagnosis(
                        bootstrapArguments,
                        DaemonDiagnosisReasonValues.ShutdownRequested,
                        "Daemon shutdown completed after shutdown request.",
                        daemonLogger);
                }
                finally
                {
                    if (serviceProvider is IDisposable disposableServiceProvider)
                    {
                        disposableServiceProvider.Dispose();
                    }
                }

                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "uCLI daemon bootstrap failed with unhandled exception.",
                    exception);
                if (!diagnosisWritten)
                {
                    diagnosisWritten = await PersistDiagnosis(
                        bootstrapArguments,
                        daemonStarted
                            ? DaemonDiagnosisReasonValues.UnhandledException
                            : DaemonDiagnosisReasonValues.StartupFailed,
                        daemonStarted
                            ? $"Daemon bootstrap failed with an unhandled exception. {exception.Message}"
                            : $"Daemon startup failed before running state was established. {exception.Message}",
                        daemonLogger);
                }

                EditorApplication.Exit(1);
            }
        }

        private static async Task<bool> PersistDiagnosis (
            IpcDaemonBootstrapArguments bootstrapArguments,
            string reason,
            string message,
            IDaemonLogger daemonLogger)
        {
            try
            {
                await DaemonDiagnosisPersistence.Write(
                    bootstrapArguments,
                    reason,
                    message,
                    CancellationToken.None);
                return true;
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "Failed to persist daemon diagnosis.",
                    exception);
                return false;
            }
        }
    }
}
