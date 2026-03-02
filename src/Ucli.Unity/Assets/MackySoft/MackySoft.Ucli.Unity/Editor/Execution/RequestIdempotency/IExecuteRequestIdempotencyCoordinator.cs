using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    /// <summary> Coordinates request-id idempotency behaviors for execute request dispatching. </summary>
    internal interface IExecuteRequestIdempotencyCoordinator
    {
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
