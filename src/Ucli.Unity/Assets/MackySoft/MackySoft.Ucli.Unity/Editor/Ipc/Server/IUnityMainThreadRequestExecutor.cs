using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Executes IPC request-handling delegates on Unity main thread context. </summary>
    internal interface IUnityMainThreadRequestExecutor
    {
        /// <summary> Executes one request-handling delegate on Unity main thread. </summary>
        /// <param name="requestHandler"> The request-handling delegate to execute. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by connection handling. </param>
        /// <returns> The handled IPC response. </returns>
        Task<IpcResponse> Execute (
            Func<Task<IpcResponse>> requestHandler,
            CancellationToken cancellationToken = default);
    }
}