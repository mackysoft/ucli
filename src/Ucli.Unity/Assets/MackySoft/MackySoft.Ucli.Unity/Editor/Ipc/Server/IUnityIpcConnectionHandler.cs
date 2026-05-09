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
        /// <returns> The handled connection exchange result. </returns>
        Task<UnityIpcConnectionHandleResult> HandleAsync (
            Stream stream,
            CancellationToken cancellationToken = default);
    }
}