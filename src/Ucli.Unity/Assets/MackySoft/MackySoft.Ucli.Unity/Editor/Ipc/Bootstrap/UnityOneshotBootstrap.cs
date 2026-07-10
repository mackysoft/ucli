using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Bootstraps one batchmode oneshot IPC server and terminates Unity after one handled request. </summary>
    internal static class UnityOneshotBootstrap
    {
        /// <summary> Starts one oneshot bootstrap after batchmode initialization is ready. </summary>
        /// <returns> A task that completes after the request finishes and process exit is requested. </returns>
        internal static async Task StartAsync (IpcOneshotBootstrapArguments bootstrapArguments)
        {
            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            var daemonLogStream = new DaemonLogRingBuffer();
            var daemonLogger = new DaemonLogger(
                daemonLogStream,
                UnityMainThreadDaemonConsoleLogSink.CaptureCurrent());
            try
            {
                using var parentProcessWatcher = OneshotParentProcessWatcher.Start(bootstrapArguments.ParentProcessId);
                using var deadlineWatcher = OneshotDeadlineWatcher.Start(bootstrapArguments.ExitDeadlineUtc);
                var services = new ServiceCollection();
                services.AddUnityIpcApplicationServices(
                    new ExactSessionTokenValidator(bootstrapArguments.SessionToken),
                    bootstrapArguments.ProjectFingerprint,
                    daemonLogger,
                    DaemonEditorMode.Batchmode);
                services.AddUnityIpcOneshotHostServices();

                IServiceProvider serviceProvider = services.BuildServiceProvider();
                try
                {
                    var completionSignal = serviceProvider.GetRequiredService<OneshotRequestCompletionSignal>();
                    var server = serviceProvider.GetRequiredService<IUnityIpcServer>();
                    if (!ContractLiteralCodec.TryParse<IpcTransportKind>(bootstrapArguments.EndpointTransportKind, out var transportKind))
                    {
                        throw new InvalidOperationException($"Unsupported endpoint transport kind: {bootstrapArguments.EndpointTransportKind}");
                    }

                    var endpoint = new IpcEndpoint(transportKind, bootstrapArguments.EndpointAddress);
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

                    if (parentProcessWatcher.HasRequestedExit)
                    {
                        await server.StopAsync(CancellationToken.None);
                        return;
                    }

                    var deadlineTask = deadlineWatcher.WaitAsync();
                    var completedTask = await Task.WhenAny(requestCompletionTask, serverTerminationTask, deadlineTask);
                    if (ReferenceEquals(completedTask, serverTerminationTask))
                    {
                        await serverTerminationTask;
                        throw new InvalidOperationException("IPC server loop terminated before oneshot request completion was observed.");
                    }

                    if (ReferenceEquals(completedTask, deadlineTask))
                    {
                        await server.StopAsync(CancellationToken.None);
                        parentProcessWatcher.Dispose();
                        EditorApplication.Exit(1);
                        return;
                    }

                    await requestCompletionTask;
                    await server.StopAsync(CancellationToken.None);
                    parentProcessWatcher.Dispose();
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
        }
    }
}
