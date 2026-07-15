using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Bootstraps one batchmode oneshot IPC server and terminates Unity after one handled request. </summary>
    internal static class UnityOneshotBootstrap
    {
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
                var endpoint = UnityBatchmodeBootstrapEndpointValidator.ResolveValidatedOneshotEndpoint(bootstrapEnvelope);
                var services = new ServiceCollection();
                services.AddUnityIpcApplicationServices(
                    new ExactSessionTokenValidator(bootstrapEnvelope.SessionToken),
                    bootstrapEnvelope.ProjectFingerprint,
                    daemonLogger,
                    DaemonEditorMode.Batchmode);
                services.AddUnityIpcOneshotHostServices(endpoint, lifetimeWatchdog);

                IServiceProvider serviceProvider = services.BuildServiceProvider();
                try
                {
                    var completionSignal = serviceProvider.GetRequiredService<OneshotRequestCompletionSignal>();
                    var server = serviceProvider.GetRequiredService<IUnityIpcServer>();
                    using var publicationFence = await server.StartAsync(endpoint, CancellationToken.None);
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
                    await server.StopAsync(CancellationToken.None);
                    EditorApplication.Exit(0);
                }
                finally
                {
                    if (serviceProvider is IDisposable disposableServiceProvider)
                    {
                        disposableServiceProvider.Dispose();
                    }
                }
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
