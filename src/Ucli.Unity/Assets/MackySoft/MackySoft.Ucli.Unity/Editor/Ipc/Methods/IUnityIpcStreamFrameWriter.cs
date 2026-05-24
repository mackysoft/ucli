using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Writes progress frames for one streaming IPC method. </summary>
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
    }
}
