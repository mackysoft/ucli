using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    /// <summary> Provides stateful storage operations used by request-id idempotency coordination flows. </summary>
    internal interface IExecuteRequestIdempotencyStore
    {
        /// <summary> Acquires one idempotency decision for an incoming request-id and digest. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <param name="requestDigest"> The deterministic request digest. </param>
        /// <returns> The decision that determines owner execution, replay, conflict, or wait behavior. </returns>
        ExecuteRequestIdempotencyStoreDecision Acquire (
            string requestId,
            string requestDigest);

        /// <summary> Completes one owner execution successfully and publishes the response to shared waiters. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <param name="requestDigest"> The deterministic request digest. </param>
        /// <param name="response"> The completed response envelope. </param>
        void CompleteSuccess (
            string requestId,
            string requestDigest,
            IpcResponse response);

        /// <summary> Completes one owner execution with cancellation and notifies shared waiters. </summary>
        /// <param name="requestId"> The request identifier. </param>
        void CompleteCanceled (string requestId);

        /// <summary> Completes one owner execution with failure and notifies shared waiters. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <param name="exception"> The execution failure exception. </param>
        void CompleteFailed (
            string requestId,
            Exception exception);
    }
}
