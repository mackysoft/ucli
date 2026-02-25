using System;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Provides a minimal Unity IPC server lifecycle skeleton for foundation tasks. </summary>
    internal sealed class UnityIpcServer : IUnityIpcServer
    {
        private bool isRunning;

        /// <summary> Gets a value indicating whether the server lifecycle is marked as started. </summary>
        public bool IsRunning => isRunning;

        /// <summary> Starts the IPC server lifecycle for the specified endpoint. </summary>
        /// <param name="endpoint"> The endpoint definition used by server binding. Must not be <see langword="null" />, and its address must not be empty or whitespace. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes after startup flag transitions to running. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="endpoint" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when endpoint address is empty or whitespace. </exception>
        public Task StartAsync (
            IpcEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (string.IsNullOrWhiteSpace(endpoint.Address))
            {
                throw new ArgumentException("Endpoint address must not be empty or whitespace.", nameof(endpoint));
            }

            // NOTE:
            // Transport binding and request processing are introduced incrementally.
            // This foundation class currently owns lifecycle state only.
            isRunning = true;
            return Task.CompletedTask;
        }

        /// <summary> Stops the IPC server lifecycle. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes after running state is cleared. </returns>
        public Task StopAsync (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            isRunning = false;
            return Task.CompletedTask;
        }
    }
}
