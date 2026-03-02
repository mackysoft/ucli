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
        /// <summary> Entry point invoked by Unity <c>-executeMethod</c> to start daemon mode. </summary>
        public static async void Start ()
        {
            try
            {
                await Run();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary> Starts IPC server and blocks until shutdown request is received. </summary>
        /// <returns> A task that completes after process-exit request has been issued. </returns>
        private static async Task Run ()
        {
            if (!IpcDaemonBootstrapArgumentsCodec.TryParse(
                    Environment.GetCommandLineArgs(),
                    out var bootstrapArguments,
                    out var parseError))
            {
                Debug.LogError(parseError.Message);
                EditorApplication.Exit(1);
                return;
            }

            if (!IpcTransportKindCodec.TryParse(bootstrapArguments.EndpointTransportKind, out var transportKind))
            {
                Debug.LogError($"Unsupported endpoint transport kind: {bootstrapArguments.EndpointTransportKind}");
                EditorApplication.Exit(1);
                return;
            }

            var services = new ServiceCollection();
            services.AddSingleton(bootstrapArguments);
            services.AddSingleton<IDaemonShutdownSignal, DaemonShutdownSignal>();
            services.AddSingleton<ISessionTokenValidator>(new FileBackedSessionTokenValidator(bootstrapArguments.SessionPath));
            services.AddSingleton<IExecuteRequestDispatcher>(static _ => CreateExecuteRequestDispatcher());
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
                    });
            });

            using var serviceProvider = services.BuildServiceProvider();
            var server = serviceProvider.GetRequiredService<IUnityIpcServer>();
            var shutdownSignal = serviceProvider.GetRequiredService<IDaemonShutdownSignal>();

            var endpoint = new IpcEndpoint(transportKind, bootstrapArguments.EndpointAddress);
            await server.Start(endpoint, CancellationToken.None);
            Debug.Log($"uCLI daemon started. repoRoot={bootstrapArguments.RepositoryRoot}, fingerprint={bootstrapArguments.ProjectFingerprint}, endpoint={bootstrapArguments.EndpointAddress}");

            var shutdownWaitTask = shutdownSignal.Wait(CancellationToken.None);
            var serverTerminationTask = server.WaitForTermination(CancellationToken.None);
            var completedTask = await Task.WhenAny(shutdownWaitTask, serverTerminationTask);
            if (ReferenceEquals(completedTask, serverTerminationTask))
            {
                await serverTerminationTask;
                throw new InvalidOperationException("IPC server loop terminated before shutdown request was received.");
            }

            await shutdownWaitTask;
            await server.Stop(CancellationToken.None);
            EditorApplication.Exit(0);
        }

        /// <summary> Creates execute-request dispatcher used by Unity daemon mode. </summary>
        /// <returns> The execute-request dispatcher instance. </returns>
        private static IExecuteRequestDispatcher CreateExecuteRequestDispatcher ()
        {
            var normalizer = new ExecuteRequestNormalizer();
            var operationRegistry = new InMemoryPhaseOperationRegistry(new IPhaseOperation[]
            {
                new ResolvePhaseOperation(),
            });
            var phaseExecutor = new OperationPhaseExecutor(operationRegistry);
            return new ExecuteRequestDispatcher(normalizer, phaseExecutor);
        }
    }
}
