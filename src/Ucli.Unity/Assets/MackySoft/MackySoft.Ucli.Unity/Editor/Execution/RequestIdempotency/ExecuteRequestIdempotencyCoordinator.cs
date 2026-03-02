using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    /// <summary> Provides in-memory request-id idempotency coordination for execute dispatching. </summary>
    internal sealed class ExecuteRequestIdempotencyCoordinator : IExecuteRequestIdempotencyCoordinator
    {
        /// <summary> Gets the default completed-response retention TTL. </summary>
        public static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(24);

        /// <summary> Gets the default maximum number of completed-response cache entries. </summary>
        public const int DefaultMaxEntries = 10000;

        private readonly object syncRoot = new object();

        private readonly Dictionary<string, CompletedEntry> completedEntries = new Dictionary<string, CompletedEntry>(StringComparer.Ordinal);

        private readonly LinkedList<string> completedOrder = new LinkedList<string>();

        private readonly Dictionary<string, InFlightEntry> inFlightEntries = new Dictionary<string, InFlightEntry>(StringComparer.Ordinal);

        private readonly TimeSpan cacheTtl;

        private readonly int maxEntries;

        private readonly Func<DateTimeOffset> utcNowProvider;

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
        {
            if (cacheTtl <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(cacheTtl), cacheTtl, "Cache TTL must be greater than zero.");
            }

            if (maxEntries <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntries), maxEntries, "Max entries must be greater than zero.");
            }

            this.cacheTtl = cacheTtl;
            this.maxEntries = maxEntries;
            this.utcNowProvider = utcNowProvider ?? throw new ArgumentNullException(nameof(utcNowProvider));
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

            Task<IpcResponse>? sharedResponseTask = null;
            TaskCompletionSource<IpcResponse>? ownerCompletionSource = null;
            var nowUtc = utcNowProvider();

            lock (syncRoot)
            {
                EvictExpiredEntries(nowUtc);

                if (completedEntries.TryGetValue(requestId, out var completedEntry))
                {
                    if (string.Equals(completedEntry.RequestDigest, requestDigest, StringComparison.Ordinal))
                    {
                        return completedEntry.Response;
                    }

                    return createConflictResponse();
                }

                if (inFlightEntries.TryGetValue(requestId, out var inFlightEntry))
                {
                    if (!string.Equals(inFlightEntry.RequestDigest, requestDigest, StringComparison.Ordinal))
                    {
                        return createConflictResponse();
                    }

                    sharedResponseTask = inFlightEntry.CompletionSource.Task;
                }
                else
                {
                    ownerCompletionSource = new TaskCompletionSource<IpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                    inFlightEntries[requestId] = new InFlightEntry(requestDigest, ownerCompletionSource);
                }
            }

            if (sharedResponseTask != null)
            {
                return await WaitForSharedResponse(sharedResponseTask, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                var response = await executeRequest(cancellationToken).ConfigureAwait(false);
                var createdAtUtc = utcNowProvider();

                lock (syncRoot)
                {
                    inFlightEntries.Remove(requestId);
                    SaveCompletedEntry(requestId, requestDigest, response, createdAtUtc);
                    ownerCompletionSource!.TrySetResult(response);
                }

                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                lock (syncRoot)
                {
                    inFlightEntries.Remove(requestId);
                    ownerCompletionSource!.TrySetCanceled();
                }

                throw;
            }
            catch (Exception exception)
            {
                lock (syncRoot)
                {
                    inFlightEntries.Remove(requestId);
                    ownerCompletionSource!.TrySetException(exception);
                }

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

        /// <summary> Saves one completed response entry and applies overflow eviction. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <param name="requestDigest"> The request digest. </param>
        /// <param name="response"> The completed response. </param>
        /// <param name="createdAtUtc"> The completion timestamp in UTC. </param>
        private void SaveCompletedEntry (
            string requestId,
            string requestDigest,
            IpcResponse response,
            DateTimeOffset createdAtUtc)
        {
            if (completedEntries.TryGetValue(requestId, out var existingEntry))
            {
                completedOrder.Remove(existingEntry.OrderNode);
            }

            var orderNode = completedOrder.AddLast(requestId);
            completedEntries[requestId] = new CompletedEntry(
                RequestDigest: requestDigest,
                Response: response,
                CreatedAtUtc: createdAtUtc,
                ExpiresAtUtc: createdAtUtc.Add(cacheTtl),
                OrderNode: orderNode);
            EvictOverflowEntries();
        }

        /// <summary> Evicts expired completed entries in chronological insertion order. </summary>
        /// <param name="nowUtc"> The current UTC timestamp. </param>
        private void EvictExpiredEntries (DateTimeOffset nowUtc)
        {
            while (completedOrder.First != null)
            {
                var requestId = completedOrder.First.Value;
                if (!completedEntries.TryGetValue(requestId, out var entry))
                {
                    completedOrder.RemoveFirst();
                    continue;
                }

                if (nowUtc <= entry.ExpiresAtUtc)
                {
                    break;
                }

                completedOrder.RemoveFirst();
                completedEntries.Remove(requestId);
            }
        }

        /// <summary> Evicts oldest completed entries while count exceeds configured max. </summary>
        private void EvictOverflowEntries ()
        {
            while (completedEntries.Count > maxEntries && completedOrder.First != null)
            {
                var oldestRequestId = completedOrder.First.Value;
                completedOrder.RemoveFirst();
                completedEntries.Remove(oldestRequestId);
            }
        }

        /// <summary> Represents one in-flight owner execution entry keyed by request-id. </summary>
        /// <param name="RequestDigest"> The digest used by the owner request. </param>
        /// <param name="CompletionSource"> The shared completion source for waiters. </param>
        private sealed record InFlightEntry (
            string RequestDigest,
            TaskCompletionSource<IpcResponse> CompletionSource);

        /// <summary> Represents one completed response cache entry keyed by request-id. </summary>
        /// <param name="RequestDigest"> The digest used by the completed request. </param>
        /// <param name="Response"> The completed response envelope. </param>
        /// <param name="CreatedAtUtc"> The completion timestamp in UTC. </param>
        /// <param name="ExpiresAtUtc"> The expiration timestamp in UTC. </param>
        /// <param name="OrderNode"> The insertion-order linked-list node. </param>
        private sealed record CompletedEntry (
            string RequestDigest,
            IpcResponse Response,
            DateTimeOffset CreatedAtUtc,
            DateTimeOffset ExpiresAtUtc,
            LinkedListNode<string> OrderNode);
    }
}
