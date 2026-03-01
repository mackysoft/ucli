using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles one IPC request-response exchange over a connected transport stream. </summary>
    internal interface IUnityIpcConnectionHandler
    {
        /// <summary> Handles one request-response exchange over a connected transport stream. </summary>
        /// <param name="stream"> The connected transport stream. </param>
        /// <param name="cancellationToken"> The cancellation token for request handling. </param>
        /// <returns> A task that completes after one response frame is written. </returns>
        Task Handle (
            Stream stream,
            CancellationToken cancellationToken = default);
    }
}
