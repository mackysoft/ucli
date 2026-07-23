using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Unity.Runtime;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Bootstraps IPC daemon server when Unity is launched in batchmode daemon mode. </summary>
    internal static class UnityDaemonBootstrap
    {
        private static IServiceProvider RetainedUnsafeServiceProvider { get; set; }

        /// <summary> Starts Unity daemon mode after batchmode initialization is ready. </summary>
        /// <returns> A task that completes after daemon mode exits or bootstrap failure requests process exit. </returns>
        internal static async Task StartAsync (IpcDaemonBootstrapArguments bootstrapArguments)
        {
            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            var editorInstanceId = UnityEditorSessionStateStore.GetOrCreateEditorInstanceId();
            var daemonLogStream = new DaemonLogRingBuffer();
            var daemonLogger = new DaemonLogger(
                daemonLogStream,
                UnityMainThreadDaemonConsoleLogSink.CaptureCurrent());
            var daemonStarted = false;
            var diagnosisWritten = false;
            UnityDaemonBootstrapContext bootstrapContext = null;

            try
            {
                bootstrapContext = UnityDaemonBootstrapContext.FromWire(bootstrapArguments);
                var endpointBinding = UnityBatchmodeBootstrapEndpointValidator.ResolveValidatedDaemonEndpoint(bootstrapContext);
                var sessionToken = await DaemonBootstrapSessionTokenResolver.ResolveAsync(
                    bootstrapContext,
                    CancellationToken.None);

                var services = new ServiceCollection();
                services
                    .AddUnityIpcApplicationServices(
                        new ExactSessionTokenValidator(sessionToken),
                        bootstrapContext.ProjectFingerprint,
                        daemonLogger,
                        DaemonEditorMode.Batchmode)
                    .AddUnityIpcDaemonHostServices(
                        bootstrapContext,
                        daemonLogStream,
                        editorInstanceId);

                IServiceProvider serviceProvider = services.BuildServiceProvider();
                IUnityIpcServer server = null;
                IUnityControlPlaneRequestLifetime controlPlaneRequestLifetime = null;
                IUnityMutationLaneControl mutationLaneControl = null;
                var generationRetiredSafely = false;
                try
                {
                    server = serviceProvider.GetRequiredService<IUnityIpcServer>();
                    controlPlaneRequestLifetime = serviceProvider
                        .GetRequiredService<IUnityControlPlaneRequestLifetime>();
                    mutationLaneControl = serviceProvider.GetRequiredService<IUnityMutationLaneControl>();
                    var shutdownSignal = serviceProvider.GetRequiredService<IDaemonShutdownSignal>();
                    var unityLogCaptureService = serviceProvider.GetRequiredService<UnityLogCaptureService>();
                    unityLogCaptureService.Start();

                    using var publicationFence = await server.StartAsync(
                        endpointBinding,
                        CancellationToken.None);
                    Task shutdownWaitTask = null;
                    Task serverTerminationTask = null;
                    if (!publicationFence.TryCommitActiveOwnership(() =>
                        {
                            daemonStarted = true;
                            shutdownWaitTask = shutdownSignal.WaitAsync(CancellationToken.None);
                            serverTerminationTask = server.WaitForTerminationAsync(CancellationToken.None);
                        }))
                    {
                        throw new InvalidOperationException(
                            "IPC listener terminated before daemon endpoint ownership could become active.");
                    }

                    daemonLogger.Info(
                        DaemonLogCategories.Lifecycle,
                        $"uCLI daemon started. repoRoot={bootstrapContext.RepositoryRoot.Value}, fingerprint={bootstrapContext.ProjectFingerprint}, endpoint={bootstrapContext.EndpointBinding.Endpoint.Address}");

                    var completedTask = await Task.WhenAny(shutdownWaitTask, serverTerminationTask);
                    if (ReferenceEquals(completedTask, serverTerminationTask))
                    {
                        await serverTerminationTask;
                        const string Message = "IPC server loop terminated before daemon shutdown signal.";
                        daemonLogger.Error(
                            DaemonLogCategories.Lifecycle,
                            Message);
                        diagnosisWritten = await PersistDiagnosisAsync(
                            bootstrapContext,
                            DaemonDiagnosisReason.ListenerTerminated,
                            Message,
                            daemonLogger);
                        throw new InvalidOperationException("IPC server loop terminated before shutdown request was received.");
                    }

                    await shutdownWaitTask;
                    daemonLogger.Info(
                        DaemonLogCategories.Lifecycle,
                        "Daemon shutdown signal received. Stopping IPC server.");
                }
                finally
                {
                    var serverStoppedSafely = false;
                    if (server != null)
                    {
                        try
                        {
                            await server.StopAsync(CancellationToken.None);
                            serverStoppedSafely = true;
                        }
                        catch (Exception exception)
                        {
                            daemonLogger.Warning(
                                DaemonLogCategories.Lifecycle,
                                $"Daemon IPC server cleanup stop failed. {exception.Message}");
                        }

                        if (serverStoppedSafely
                            && controlPlaneRequestLifetime != null
                            && mutationLaneControl != null)
                        {
                            try
                            {
                                var retirementTask = Task.WhenAll(
                                    mutationLaneControl.WaitForRetirementAsync(),
                                    controlPlaneRequestLifetime.WaitForRetirementAsync());
                                generationRetiredSafely = await UnityHostGenerationRetirementPolicy
                                    .WaitWithinForegroundDeadlineAsync(retirementTask);
                                if (!generationRetiredSafely)
                                {
                                    daemonLogger.Warning(
                                        DaemonLogCategories.Lifecycle,
                                        $"Daemon request execution retirement exceeded its {UnityHostGenerationRetirementPolicy.ForegroundDeadline.TotalMilliseconds:0}ms foreground deadline.");
                                }
                            }
                            catch (Exception exception)
                            {
                                daemonLogger.Warning(
                                    DaemonLogCategories.Lifecycle,
                                    $"Daemon request execution retirement failed. {exception.Message}");
                            }
                        }
                    }

                    if (server == null || generationRetiredSafely)
                    {
                        if (serviceProvider is IDisposable disposableServiceProvider)
                        {
                            disposableServiceProvider.Dispose();
                        }
                    }
                    else
                    {
                        RetainedUnsafeServiceProvider = serviceProvider;
                        daemonLogger.Warning(
                            DaemonLogCategories.Lifecycle,
                            "Daemon service provider is retained until process exit because its IPC generation did not retire safely.");
                    }
                }

                if (!generationRetiredSafely)
                {
                    throw new InvalidOperationException(
                        "Daemon IPC generation did not retire safely during shutdown.");
                }

                daemonLogger.Info(
                    DaemonLogCategories.Lifecycle,
                    "IPC server stop and request execution retirement completed. Exiting Unity batchmode process.");
                diagnosisWritten = await PersistDiagnosisAsync(
                    bootstrapContext,
                    DaemonDiagnosisReason.ShutdownRequested,
                    "Daemon shutdown completed after shutdown request.",
                    daemonLogger);

                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "uCLI daemon bootstrap failed with unhandled exception.",
                    exception);
                if (!diagnosisWritten && bootstrapContext != null)
                {
                    diagnosisWritten = await PersistDiagnosisAsync(
                        bootstrapContext,
                        daemonStarted
                            ? DaemonDiagnosisReason.UnhandledException
                            : DaemonDiagnosisReason.StartupFailed,
                        daemonStarted
                            ? $"Daemon bootstrap failed with an unhandled exception. {exception.Message}"
                            : $"Daemon startup failed before running state was established. {exception.Message}",
                        daemonLogger);
                }

                EditorApplication.Exit(1);
            }
        }

        private static async Task<bool> PersistDiagnosisAsync (
            UnityDaemonBootstrapContext bootstrapContext,
            DaemonDiagnosisReason reason,
            string message,
            IDaemonLogger daemonLogger)
        {
            try
            {
                await DaemonDiagnosisPersistence.WriteAsync(
                    bootstrapContext,
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
