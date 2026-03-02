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

        /// <summary> Executes one request under request-id idempotency control. </summary>
        /// <param name="requestId"> The request identifier used as idempotency key. </param>
        /// <param name="requestDigest"> The deterministic digest of request payload content. </param>
        /// <param name="executeRequest"> The owner execution callback when request should be executed. </param>
        /// <param name="createConflictResponse"> The callback that creates one conflict response for digest mismatch cases. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by dispatching pipelines. </param>
        /// <returns> The response selected by idempotency logic. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestId" />, <paramref name="requestDigest" />, <paramref name="executeRequest" />, or <paramref name="createConflictResponse" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> or <paramref name="requestDigest" /> is empty or whitespace. </exception>
        /// <exception cref="System.OperationCanceledException"> Thrown when operation is canceled. </exception>
        public async Task<IpcResponse> Execute (
            string requestId,
            string requestDigest,
            Func<CancellationToken, Task<IpcResponse>> executeRequest,
            Func<IpcResponse> createConflictResponse,
            CancellationToken cancellationToken = default)
        {
            if (requestId == null)
            {
                throw new ArgumentNullException(nameof(requestId));
            }

            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException("Request id must not be empty or whitespace.", nameof(requestId));
            }

            if (requestDigest == null)
            {
                throw new ArgumentNullException(nameof(requestDigest));
            }

            if (string.IsNullOrWhiteSpace(requestDigest))
            {
                throw new ArgumentException("Request digest must not be empty or whitespace.", nameof(requestDigest));
            }

            if (executeRequest == null)
            {
                throw new ArgumentNullException(nameof(executeRequest));
            }

            if (createConflictResponse == null)
            {
                throw new ArgumentNullException(nameof(createConflictResponse));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var decision = store.Acquire(requestId, requestDigest);
            switch (decision.Kind)
            {
                case ExecuteRequestIdempotencyStoreDecision.DecisionKind.ReplayCompleted:
                    return decision.Response!;

                case ExecuteRequestIdempotencyStoreDecision.DecisionKind.Conflict:
                    return createConflictResponse();

                case ExecuteRequestIdempotencyStoreDecision.DecisionKind.WaitInFlight:
                    return await WaitForSharedResponse(decision.SharedResponseTask!, cancellationToken).ConfigureAwait(false);

                case ExecuteRequestIdempotencyStoreDecision.DecisionKind.ExecuteOwner:
                    break;

                default:
                    throw new InvalidOperationException($"Unknown idempotency decision kind: {decision.Kind}.");
            }

            try
            {
                var response = await executeRequest(cancellationToken).ConfigureAwait(false);
                store.CompleteSuccess(requestId, requestDigest, response);
                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                store.CompleteCanceled(requestId);
                throw;
            }
            catch (Exception exception)
            {
                store.CompleteFailed(requestId, exception);
                throw;
            }
        }

        /// <summary> Waits for one owner execution result while preserving caller-local cancellation. </summary>
        /// <param name="sharedResponseTask"> The owner execution task shared by callers. </param>
        /// <param name="cancellationToken"> The caller cancellation token. </param>
        /// <returns> The owner execution response. </returns>
        /// <exception cref="System.OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled before owner completion. </exception>
        private static async Task<IpcResponse> WaitForSharedResponse (
            Task<IpcResponse> sharedResponseTask,
            CancellationToken cancellationToken)
        {
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
