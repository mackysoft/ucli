using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    /// <summary> Provides stateful storage operations used by request-id idempotency coordination flows. </summary>
    internal interface IExecuteRequestIdempotencyStore
    {
        /// <summary> Acquires one idempotency decision for an incoming request-id and fingerprint. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <param name="requestFingerprint"> The deterministic request fingerprint. </param>
        /// <returns> The decision that determines owner execution, replay, conflict, or wait behavior. </returns>
        ExecuteRequestIdempotencyStoreDecision Acquire (
            Guid requestId,
            string requestFingerprint);

        /// <summary> Completes one owner execution successfully and publishes the response to shared waiters. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <param name="requestFingerprint"> The deterministic request fingerprint. </param>
        /// <param name="response"> The completed response envelope. </param>
        void CompleteSuccess (
            Guid requestId,
            string requestFingerprint,
            IpcResponse response);

        /// <summary> Completes one owner execution with cancellation and notifies shared waiters. </summary>
        /// <param name="requestId"> The request identifier. </param>
        void CompleteCanceled (Guid requestId);

        /// <summary> Completes one owner execution with failure and notifies shared waiters. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <param name="exception"> The execution failure exception. </param>
        void CompleteFailed (
            Guid requestId,
            Exception exception);
    }
}
