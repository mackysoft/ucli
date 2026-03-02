using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    /// <summary> Coordinates request-id idempotency behaviors for execute request dispatching. </summary>
    internal interface IExecuteRequestIdempotencyCoordinator
    {
        /// <summary> Executes one request under request-id idempotency control. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <param name="requestDigest"> The deterministic digest of request payload content. </param>
        /// <param name="executeRequest"> The owner execution callback when request should be executed. </param>
        /// <param name="createConflictResponse"> The callback that creates one conflict response for digest mismatch cases. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by dispatching pipelines. </param>
        /// <returns> The response selected by idempotency logic. </returns>
        /// <exception cref="System.OperationCanceledException"> Thrown when operation is canceled. </exception>
        Task<IpcResponse> Execute (
            string requestId,
            string requestDigest,
            Func<CancellationToken, Task<IpcResponse>> executeRequest,
            Func<IpcResponse> createConflictResponse,
            CancellationToken cancellationToken = default);
    }
}
