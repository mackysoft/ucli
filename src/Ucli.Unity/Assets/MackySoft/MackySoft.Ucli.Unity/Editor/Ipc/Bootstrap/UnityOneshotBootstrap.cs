using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Bootstraps one batchmode oneshot IPC request and terminates Unity. </summary>
    internal static class UnityOneshotBootstrap
    {
        /// <summary> Starts one oneshot bootstrap after batchmode initialization is ready. </summary>
        /// <returns> A task that completes after the request finishes and process exit is requested. </returns>
        internal static Task Start (IpcOneshotBootstrapArguments bootstrapArguments)
        {
            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            return RunSafely(bootstrapArguments);
        }

        private static async Task RunSafely (IpcOneshotBootstrapArguments bootstrapArguments)
        {
            try
            {
                await Run(bootstrapArguments);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static async Task Run (IpcOneshotBootstrapArguments bootstrapArguments)
        {
            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            var request = await ReadRequest(bootstrapArguments.RequestPath);
            var responseDirectoryPath = Path.GetDirectoryName(bootstrapArguments.ResponsePath);
            if (!string.IsNullOrWhiteSpace(responseDirectoryPath))
            {
                Directory.CreateDirectory(responseDirectoryPath);
            }

            var services = new ServiceCollection();
            services.AddUnityIpcApplicationServices(
                new PermitAllSessionTokenValidator(),
                NoOpDaemonLogger.Instance);

            using var serviceProvider = services.BuildServiceProvider();
            var requestProcessor = serviceProvider.GetRequiredService<IUnityIpcRequestProcessor>();
            var response = await requestProcessor.Process(
                    request,
                    CancellationToken.None);

            await WriteResponse(
                    bootstrapArguments.ResponsePath,
                    response);
            EditorApplication.Exit(0);
        }

        private static async Task<IpcRequest> ReadRequest (string requestPath)
        {
            if (string.IsNullOrWhiteSpace(requestPath))
            {
                throw new ArgumentException("Request path must not be empty.", nameof(requestPath));
            }

            var json = await File.ReadAllTextAsync(requestPath);
            var request = JsonSerializer.Deserialize<IpcRequest>(json, IpcJsonSerializerOptions.Default);
            if (request == null)
            {
                throw new InvalidOperationException("Unity oneshot IPC request is invalid.");
            }

            return request;
        }

        private static async Task WriteResponse (
            string responsePath,
            IpcResponse response)
        {
            if (string.IsNullOrWhiteSpace(responsePath))
            {
                throw new ArgumentException("Response path must not be empty.", nameof(responsePath));
            }

            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            var directoryPath = Path.GetDirectoryName(responsePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var json = JsonSerializer.Serialize(response, IpcJsonSerializerOptions.Default);
            await File.WriteAllTextAsync(
                responsePath,
                json + Environment.NewLine);
        }
    }
}
