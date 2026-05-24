using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Writes progress and terminal frames for one streaming IPC request. </summary>
    internal interface IUnityIpcStreamFrameWriter
    {
        /// <summary> Writes one progress frame. </summary>
        /// <param name="eventName"> The progress event name. </param>
        /// <param name="payload"> The progress payload. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by the connection. </param>
        /// <returns> A task that completes after the frame is flushed. </returns>
        Task WriteProgressAsync (
            string eventName,
            object payload,
            CancellationToken cancellationToken = default);

        /// <summary> Writes the terminal response frame. </summary>
        /// <param name="response"> The terminal IPC response. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by the connection. </param>
        /// <returns> A task that completes after the frame is flushed. </returns>
        Task WriteTerminalAsync (
            IpcResponse response,
            CancellationToken cancellationToken = default);
    }
}
