using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    /// <summary> Coordinates request-id idempotency behaviors for execute request dispatching. </summary>
    internal interface IExecuteRequestIdempotencyCoordinator
    {
        /// <summary> Executes one request under request-id idempotency coordination. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <param name="requestFingerprint"> The deterministic fingerprint of request payload content. </param>
        /// <param name="executeRequest"> The owner execution delegate that runs when this caller owns the request-id entry. </param>
        /// <param name="createConflictResponse"> The factory used when the request-id conflicts with different request content. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The coordinated response envelope. </returns>
        Task<IpcResponse> Execute (
            string requestId,
            string requestFingerprint,
            Func<CancellationToken, Task<IpcResponse>> executeRequest,
            Func<IpcResponse> createConflictResponse,
            CancellationToken cancellationToken = default);

        /// <summary> Acquires one idempotency decision for request-id and fingerprint pair. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <param name="requestFingerprint"> The deterministic fingerprint of request payload content. </param>
        /// <returns> The idempotency decision for this request. </returns>
        ExecuteRequestIdempotencyStoreDecision Acquire (
            string requestId,
            string requestFingerprint);

        /// <summary> Completes one owner execution successfully and publishes response for shared waiters. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <param name="requestFingerprint"> The deterministic fingerprint of request payload content. </param>
        /// <param name="response"> The completed response envelope. </param>
        void CompleteSuccess (
            string requestId,
            string requestFingerprint,
            IpcResponse response);

        /// <summary> Completes one owner execution with cancellation and notifies shared waiters. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        void CompleteCanceled (string requestId);

        /// <summary> Completes one owner execution with failure and notifies shared waiters. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <param name="exception"> The execution failure exception. </param>
        void CompleteFailed (
            string requestId,
            Exception exception);
    }
}
