using System;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    /// <summary> Coordinates request-id idempotency behaviors for execute dispatching flows. </summary>
    internal sealed class ExecuteRequestIdempotencyCoordinator : IExecuteRequestIdempotencyCoordinator
    {
        /// <summary> Gets the default completed-response retention TTL. </summary>
        public static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(24);

        /// <summary> Gets the default maximum number of completed-response cache entries. </summary>
        public const int DefaultMaxEntries = 10000;

        private readonly IExecuteRequestIdempotencyStore store;

        /// <summary> Initializes a new instance of the <see cref="ExecuteRequestIdempotencyCoordinator" /> class with default options. </summary>
        public ExecuteRequestIdempotencyCoordinator ()
            : this(DefaultCacheTtl, DefaultMaxEntries, static () => DateTimeOffset.UtcNow)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="ExecuteRequestIdempotencyCoordinator" /> class. </summary>
        /// <param name="cacheTtl"> The cache TTL duration. Must be greater than <see cref="TimeSpan.Zero" />. </param>
        /// <param name="maxEntries"> The maximum number of completed entries retained in memory. Must be greater than zero. </param>
        /// <param name="utcNowProvider"> The UTC clock provider. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="cacheTtl" /> or <paramref name="maxEntries" /> is invalid. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="utcNowProvider" /> is <see langword="null" />. </exception>
        public ExecuteRequestIdempotencyCoordinator (
            TimeSpan cacheTtl,
            int maxEntries,
            Func<DateTimeOffset> utcNowProvider)
            : this(new InMemoryExecuteRequestIdempotencyStore(cacheTtl, maxEntries, utcNowProvider))
        {
        }

        /// <summary> Initializes a new instance of the <see cref="ExecuteRequestIdempotencyCoordinator" /> class. </summary>
        /// <param name="store"> The request-id idempotency store dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="store" /> is <see langword="null" />. </exception>
        internal ExecuteRequestIdempotencyCoordinator (IExecuteRequestIdempotencyStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary> Acquires one idempotency decision for request-id and digest pair. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <param name="requestDigest"> The deterministic digest of request payload content. </param>
        /// <returns> The idempotency decision for this request. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestId" /> or <paramref name="requestDigest" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> or <paramref name="requestDigest" /> is empty or whitespace. </exception>
        public ExecuteRequestIdempotencyStoreDecision Acquire (
            string requestId,
            string requestDigest)
        {
            ValidateRequestId(requestId);
            ValidateRequestDigest(requestDigest);
            return store.Acquire(requestId, requestDigest);
        }

        /// <summary> Completes one owner execution successfully and publishes response for shared waiters. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <param name="requestDigest"> The deterministic digest of request payload content. </param>
        /// <param name="response"> The completed response envelope. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestId" />, <paramref name="requestDigest" />, or <paramref name="response" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> or <paramref name="requestDigest" /> is empty or whitespace. </exception>
        public void CompleteSuccess (
            string requestId,
            string requestDigest,
            IpcResponse response)
        {
            ValidateRequestId(requestId);
            ValidateRequestDigest(requestDigest);

            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            store.CompleteSuccess(requestId, requestDigest, response);
        }

        /// <summary> Completes one owner execution with cancellation and notifies shared waiters. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestId" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty or whitespace. </exception>
        public void CompleteCanceled (string requestId)
        {
            ValidateRequestId(requestId);
            store.CompleteCanceled(requestId);
        }

        /// <summary> Completes one owner execution with failure and notifies shared waiters. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <param name="exception"> The execution failure exception. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestId" /> or <paramref name="exception" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty or whitespace. </exception>
        public void CompleteFailed (
            string requestId,
            Exception exception)
        {
            ValidateRequestId(requestId);

            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            store.CompleteFailed(requestId, exception);
        }

        /// <summary> Validates request-id input constraints. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestId" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty or whitespace. </exception>
        private static void ValidateRequestId (string requestId)
        {
            if (requestId == null)
            {
                throw new ArgumentNullException(nameof(requestId));
            }

            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException("Request id must not be empty or whitespace.", nameof(requestId));
            }
        }

        /// <summary> Validates request digest input constraints. </summary>
        /// <param name="requestDigest"> The deterministic request digest. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestDigest" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="requestDigest" /> is empty or whitespace. </exception>
        private static void ValidateRequestDigest (string requestDigest)
        {
            if (requestDigest == null)
            {
                throw new ArgumentNullException(nameof(requestDigest));
            }

            if (string.IsNullOrWhiteSpace(requestDigest))
            {
                throw new ArgumentException("Request digest must not be empty or whitespace.", nameof(requestDigest));
            }
        }
    }
}
