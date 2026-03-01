using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Bootstraps IPC daemon server when Unity is launched in batchmode daemon mode. </summary>
    internal static class UnityDaemonBootstrap
    {
        /// <summary> Entry point invoked by Unity <c>-executeMethod</c> to start daemon mode. </summary>
        public static void Start ()
        {
            try
            {
                Run().GetAwaiter().GetResult();
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
            IDaemonBootstrapArgumentsParser parser = new DaemonBootstrapArgumentsParser();
            IUnityDaemonServiceProviderFactory serviceProviderFactory = new UnityDaemonServiceProviderFactory();
            if (!parser.TryParse(Environment.GetCommandLineArgs(), out var bootstrapArguments, out var parseErrorMessage))
            {
                Debug.LogError(parseErrorMessage);
                EditorApplication.Exit(1);
                return;
            }

            if (!UnityIpcTransportKindCodec.TryParse(bootstrapArguments.EndpointTransportKind, out var transportKind))
            {
                Debug.LogError($"Unsupported endpoint transport kind: {bootstrapArguments.EndpointTransportKind}");
                EditorApplication.Exit(1);
                return;
            }

            var shutdownSignalSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var serviceProvider = serviceProviderFactory.Create(
                bootstrapArguments,
                () => shutdownSignalSource.TrySetResult(true));
            var server = serviceProvider.GetRequiredService<IUnityIpcServer>();

            var endpoint = new IpcEndpoint(transportKind, bootstrapArguments.EndpointAddress);
            await server.Start(endpoint, CancellationToken.None);
            Debug.Log($"uCLI daemon started. repoRoot={bootstrapArguments.RepositoryRoot}, fingerprint={bootstrapArguments.ProjectFingerprint}, endpoint={bootstrapArguments.EndpointAddress}");

            await shutdownSignalSource.Task;
            await server.Stop(CancellationToken.None);
            EditorApplication.Exit(0);
        }
    }
}
