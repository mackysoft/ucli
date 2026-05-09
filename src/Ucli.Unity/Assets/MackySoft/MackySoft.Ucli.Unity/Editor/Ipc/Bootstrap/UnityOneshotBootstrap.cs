using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;
using UnityEngine;

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

            try
            {
                using var parentProcessWatcher = OneshotParentProcessWatcher.Start(bootstrapArguments.ParentProcessId);
                using var deadlineWatcher = OneshotDeadlineWatcher.Start(bootstrapArguments.ExitDeadlineUtc);
                var services = new ServiceCollection();
                services.AddUnityIpcApplicationServices(
                    new ExactSessionTokenValidator(bootstrapArguments.SessionToken),
                    bootstrapArguments.ProjectFingerprint,
                    NoOpDaemonLogger.Instance);
                services.AddUnityIpcOneshotHostServices();

                IServiceProvider serviceProvider = services.BuildServiceProvider();
                try
                {
                    var completionSignal = serviceProvider.GetRequiredService<OneshotRequestCompletionSignal>();
                    var server = serviceProvider.GetRequiredService<IUnityIpcServer>();
                    if (!IpcTransportKindCodec.TryParse(bootstrapArguments.EndpointTransportKind, out var transportKind))
                    {
                        throw new InvalidOperationException($"Unsupported endpoint transport kind: {bootstrapArguments.EndpointTransportKind}");
                    }

                    var endpoint = new IpcEndpoint(transportKind, bootstrapArguments.EndpointAddress);
                    await server.StartAsync(endpoint, CancellationToken.None);
                    if (parentProcessWatcher.HasRequestedExit)
                    {
                        await server.StopAsync(CancellationToken.None);
                        return;
                    }

                    var requestCompletionTask = completionSignal.WaitAsync(CancellationToken.None);
                    var serverTerminationTask = server.WaitForTerminationAsync(CancellationToken.None);
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
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
