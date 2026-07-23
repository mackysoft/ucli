using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;
using MackySoft.Ucli.Unity.Runtime;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Bootstraps one batchmode oneshot IPC server and terminates Unity after one handled request. </summary>
    internal static class UnityOneshotBootstrap
    {
        private static IServiceProvider RetainedUnsafeServiceProvider { get; set; }

        /// <summary> Starts one oneshot bootstrap after batchmode initialization is ready. </summary>
        /// <param name="bootstrapEnvelope"> The validated bootstrap generation owned by this process. </param>
        /// <param name="lifetimeWatchdog"> The watchdog instance started for the same bootstrap generation. </param>
        /// <returns> A task that completes after the request finishes and process exit is requested. </returns>
        internal static async Task StartAsync (
            IpcOneshotBootstrapEnvelope bootstrapEnvelope,
            OneshotProcessLifetimeWatchdog lifetimeWatchdog)
        {
            if (bootstrapEnvelope == null)
            {
                throw new ArgumentNullException(nameof(bootstrapEnvelope));
            }

            if (lifetimeWatchdog == null)
            {
                throw new ArgumentNullException(nameof(lifetimeWatchdog));
            }

            var daemonLogStream = new DaemonLogRingBuffer();
            var daemonLogger = new DaemonLogger(
                daemonLogStream,
                UnityMainThreadDaemonConsoleLogSink.CaptureCurrent());
            try
            {
                var endpointBinding = UnityBatchmodeBootstrapEndpointValidator.ResolveValidatedOneshotEndpoint(bootstrapEnvelope);
                var services = new ServiceCollection();
                services.AddUnityIpcApplicationServices(
                    new ExactSessionTokenValidator(bootstrapEnvelope.SessionToken),
                    bootstrapEnvelope.ProjectFingerprint,
                    daemonLogger,
                    DaemonEditorMode.Batchmode);
                services.AddUnityIpcOneshotHostServices(endpointBinding, lifetimeWatchdog);

                IServiceProvider serviceProvider = services.BuildServiceProvider();
                IUnityIpcServer server = null;
                IUnityControlPlaneRequestLifetime controlPlaneRequestLifetime = null;
                IUnityMutationLaneControl mutationLaneControl = null;
                var generationRetiredSafely = false;
                try
                {
                    var completionSignal = serviceProvider.GetRequiredService<OneshotRequestCompletionSignal>();
                    server = serviceProvider.GetRequiredService<IUnityIpcServer>();
                    controlPlaneRequestLifetime = serviceProvider
                        .GetRequiredService<IUnityControlPlaneRequestLifetime>();
                    mutationLaneControl = serviceProvider.GetRequiredService<IUnityMutationLaneControl>();
                    using var publicationFence = await server.StartAsync(
                        endpointBinding,
                        CancellationToken.None);
                    Task requestCompletionTask = null;
                    Task serverTerminationTask = null;
                    if (!publicationFence.TryCommitActiveOwnership(() =>
                        {
                            requestCompletionTask = completionSignal.WaitAsync(CancellationToken.None);
                            serverTerminationTask = server.WaitForTerminationAsync(CancellationToken.None);
                        }))
                    {
                        throw new InvalidOperationException(
                            "IPC listener terminated before oneshot endpoint ownership could become active.");
                    }

                    var completedTask = await Task.WhenAny(requestCompletionTask, serverTerminationTask);
                    if (ReferenceEquals(completedTask, serverTerminationTask))
                    {
                        await serverTerminationTask;
                        throw new InvalidOperationException("IPC server loop terminated before oneshot request completion was observed.");
                    }

                    await requestCompletionTask;
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
                                $"Oneshot IPC server cleanup stop failed. {exception.Message}");
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
                                        $"Oneshot request execution retirement exceeded its {UnityHostGenerationRetirementPolicy.ForegroundDeadline.TotalMilliseconds:0}ms foreground deadline.");
                                }
                            }
                            catch (Exception exception)
                            {
                                daemonLogger.Warning(
                                    DaemonLogCategories.Lifecycle,
                                    $"Oneshot request execution retirement failed. {exception.Message}");
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
                            "Oneshot service provider is retained until process exit because its IPC generation did not retire safely.");
                    }
                }

                if (!generationRetiredSafely)
                {
                    throw new InvalidOperationException(
                        "Oneshot IPC generation did not retire safely after request completion.");
                }

                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "uCLI oneshot bootstrap failed.",
                    exception);
                EditorApplication.Exit(1);
            }
            finally
            {
                DeleteBootstrapEnvelopeIfOwned(bootstrapEnvelope);
            }
        }

        private static void DeleteBootstrapEnvelopeIfOwned (IpcOneshotBootstrapEnvelope bootstrapEnvelope)
        {
            try
            {
                var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
                var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectRoot);
                OneshotBootstrapEnvelopeStore.TryDeleteIfOwned(storageRoot, bootstrapEnvelope);
            }
            catch (Exception)
            {
                // NOTE: The CLI process-handle owner independently performs ownership-checked cleanup.
            }
        }
    }
}
