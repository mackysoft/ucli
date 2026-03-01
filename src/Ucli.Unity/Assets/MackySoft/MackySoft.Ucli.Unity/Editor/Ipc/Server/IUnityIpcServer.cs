using System;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Starts and stops Unity-side IPC listener lifecycles. </summary>
    internal interface IUnityIpcServer
    {
        /// <summary> Starts the server listener for the specified endpoint. </summary>
        /// <param name="endpoint"> The endpoint definition used by server binding. Must not be <see langword="null" />, and its address must not be empty or whitespace. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes when startup sequence has finished. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="endpoint" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when endpoint address is empty or whitespace. </exception>
        Task Start (
            IpcEndpoint endpoint,
            CancellationToken cancellationToken = default);

        /// <summary> Stops the server listener and releases endpoint resources. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes when stop sequence has finished. </returns>
        Task Stop (CancellationToken cancellationToken = default);
    }
}
