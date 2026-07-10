using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Starts and stops Unity-side IPC listener lifecycles. </summary>
    internal interface IUnityIpcServer
    {
        /// <summary> Starts the server listener for the specified endpoint. </summary>
        /// <param name="endpoint"> The endpoint definition used by server binding. Must not be <see langword="null" />, and its address must not be empty or whitespace. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that produces the listener generation fence required before durable endpoint publication. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="endpoint" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when endpoint address is empty or whitespace. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when a listener is already active or a prior listener generation did not terminate safely. </exception>
        Task<IUnityIpcServerPublicationFence> StartAsync (
            IpcEndpoint endpoint,
            CancellationToken cancellationToken = default);

        /// <summary> Stops the server listener and releases endpoint resources. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes when stop sequence has finished. </returns>
        /// <exception cref="TimeoutException"> Thrown when the listener generation does not terminate before the configured stop deadline. </exception>
        Task StopAsync (CancellationToken cancellationToken = default);

        /// <summary> Synchronously releases transport handles from Unity editor lifecycle callbacks without waiting for connection tasks. </summary>
        void ReleaseForEditorLifecycleEvent ();

        /// <summary> Waits until the active listener loop terminates. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes when listener loop terminates, or immediately when server has not been started. </returns>
        /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled before listener loop terminates. </exception>
        Task WaitForTerminationAsync (CancellationToken cancellationToken = default);
    }
}
