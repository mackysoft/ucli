using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles one IPC request-response exchange over a connected transport stream. </summary>
    internal interface IUnityIpcConnectionHandler
    {
        /// <summary> Handles one request-response exchange over a connected transport stream. </summary>
        /// <param name="stream"> The connected transport stream. </param>
        /// <param name="cancellationToken"> The cancellation token for request handling. </param>
        /// <returns> The handled request when one request frame was decoded successfully; otherwise <see langword="null" />. </returns>
        Task<IpcRequest?> Handle (
            Stream stream,
            CancellationToken cancellationToken = default);
    }
}
