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
        internal static async Task Start (IpcOneshotBootstrapArguments bootstrapArguments)
        {
            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            try
            {
                using var parentProcessWatcher = OneshotParentProcessWatcher.Start(bootstrapArguments.ParentProcessId);
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
                    await server.Start(endpoint, CancellationToken.None);
                    if (parentProcessWatcher.HasRequestedExit)
                    {
                        await server.Stop(CancellationToken.None);
                        return;
                    }

                    var requestCompletionTask = completionSignal.Wait(CancellationToken.None);
                    var serverTerminationTask = server.WaitForTermination(CancellationToken.None);
                    var completedTask = await Task.WhenAny(requestCompletionTask, serverTerminationTask);
                    if (ReferenceEquals(completedTask, serverTerminationTask))
                    {
                        await serverTerminationTask;
                        throw new InvalidOperationException("IPC server loop terminated before oneshot request completion was observed.");
                    }

                    await requestCompletionTask;
                    await server.Stop(CancellationToken.None);
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
