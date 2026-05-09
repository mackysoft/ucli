using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Dispatch
{
    /// <summary> Dispatches <c>execute</c> IPC requests into Unity operation pipelines. </summary>
    internal interface IExecuteRequestDispatcher
    {
        /// <summary> Dispatches one execute request and returns the response envelope. </summary>
        /// <param name="request"> The execute request payload. </param>
        /// <param name="context"> The request-level dispatch context. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The response envelope for the incoming request. </returns>
        /// <exception cref="System.OperationCanceledException"> Thrown when dispatch is canceled. </exception>
        Task<IpcResponse> DispatchAsync (
            IpcExecuteRequest request,
            ExecuteDispatchContext context,
            CancellationToken cancellationToken = default);
    }
}