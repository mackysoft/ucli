using System;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary> Executes one request under request-id idempotency coordination. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <param name="requestFingerprint"> The deterministic fingerprint of request payload content. </param>
        /// <param name="executeRequest"> The owner execution delegate that runs when this caller owns the request-id entry. </param>
        /// <param name="createConflictResponse"> The factory used when the request-id conflicts with different request content. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The coordinated response envelope. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="executeRequest" /> or <paramref name="createConflictResponse" /> is <see langword="null" />. </exception>
        public async Task<IpcResponse> Execute (
            string requestId,
            string requestFingerprint,
            Func<CancellationToken, Task<IpcResponse>> executeRequest,
            Func<IpcResponse> createConflictResponse,
            CancellationToken cancellationToken = default)
        {
            if (executeRequest == null)
            {
                throw new ArgumentNullException(nameof(executeRequest));
            }

            if (createConflictResponse == null)
            {
                throw new ArgumentNullException(nameof(createConflictResponse));
            }

            cancellationToken.ThrowIfCancellationRequested();
            ExecuteRequestIdempotencyStoreDecision decision = Acquire(requestId, requestFingerprint);

            switch (decision.Kind)
            {
                case ExecuteRequestIdempotencyStoreDecision.DecisionKind.ExecuteOwner:
                    try
                    {
                        IpcResponse response = await executeRequest(cancellationToken).ConfigureAwait(false);

                        if (response == null)
                        {
                            throw new InvalidOperationException("Execute delegate returned null response.");
                        }

                        CompleteSuccess(requestId, requestFingerprint, response);
                        return response;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        CompleteCanceled(requestId);
                        throw;
                    }
                    catch (Exception exception)
                    {
                        CompleteFailed(requestId, exception);
                        throw;
                    }

                case ExecuteRequestIdempotencyStoreDecision.DecisionKind.ReplayCompleted:
                    if (decision.Response == null)
                    {
                        throw new InvalidOperationException("Replay decision did not contain a response.");
                    }

                    return decision.Response;

                case ExecuteRequestIdempotencyStoreDecision.DecisionKind.WaitInFlight:
                    if (decision.SharedResponseTask == null)
                    {
                        throw new InvalidOperationException("Wait decision did not contain a shared response task.");
                    }

                    return await WaitForSharedResponse(decision.SharedResponseTask, cancellationToken).ConfigureAwait(false);

                case ExecuteRequestIdempotencyStoreDecision.DecisionKind.Conflict:
                    return createConflictResponse();

                default:
                    throw new InvalidOperationException($"Unsupported decision kind: {decision.Kind}");
            }
        }

        /// <summary> Acquires one idempotency decision for request-id and fingerprint pair. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <param name="requestFingerprint"> The deterministic fingerprint of request payload content. </param>
        /// <returns> The idempotency decision for this request. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestId" /> or <paramref name="requestFingerprint" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> or <paramref name="requestFingerprint" /> is empty or whitespace. </exception>
        public ExecuteRequestIdempotencyStoreDecision Acquire (
            string requestId,
            string requestFingerprint)
        {
            ValidateRequestId(requestId);
            ValidateRequestFingerprint(requestFingerprint);
            return store.Acquire(requestId, requestFingerprint);
        }

        /// <summary> Completes one owner execution successfully and publishes response for shared waiters. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <param name="requestFingerprint"> The deterministic fingerprint of request payload content. </param>
        /// <param name="response"> The completed response envelope. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestId" />, <paramref name="requestFingerprint" />, or <paramref name="response" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> or <paramref name="requestFingerprint" /> is empty or whitespace. </exception>
        public void CompleteSuccess (
            string requestId,
            string requestFingerprint,
            IpcResponse response)
        {
            ValidateRequestId(requestId);
            ValidateRequestFingerprint(requestFingerprint);

            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            store.CompleteSuccess(requestId, requestFingerprint, response);
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

        /// <summary> Validates request fingerprint input constraints. </summary>
        /// <param name="requestFingerprint"> The deterministic request fingerprint. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestFingerprint" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="requestFingerprint" /> is empty or whitespace. </exception>
        private static void ValidateRequestFingerprint (string requestFingerprint)
        {
            if (requestFingerprint == null)
            {
                throw new ArgumentNullException(nameof(requestFingerprint));
            }

            if (string.IsNullOrWhiteSpace(requestFingerprint))
            {
                throw new ArgumentException("Request fingerprint must not be empty or whitespace.", nameof(requestFingerprint));
            }
        }

        /// <summary> Waits for one in-flight owner response while preserving waiter-local cancellation. </summary>
        /// <param name="sharedResponseTask"> The owner execution task shared by waiters. </param>
        /// <param name="cancellationToken"> The waiter-local cancellation token. </param>
        /// <returns> The owner execution response. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="sharedResponseTask" /> is <see langword="null" />. </exception>
        /// <exception cref="OperationCanceledException"> Thrown when the waiter is canceled before owner completion. </exception>
        private static async Task<IpcResponse> WaitForSharedResponse (
            Task<IpcResponse> sharedResponseTask,
            CancellationToken cancellationToken)
        {
            if (sharedResponseTask == null)
            {
                throw new ArgumentNullException(nameof(sharedResponseTask));
            }

            if (!cancellationToken.CanBeCanceled)
            {
                return await sharedResponseTask.ConfigureAwait(false);
            }

            var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var completedTask = await Task.WhenAny(sharedResponseTask, cancellationTask).ConfigureAwait(false);
            if (!ReferenceEquals(completedTask, sharedResponseTask))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return await sharedResponseTask.ConfigureAwait(false);
        }
    }
}