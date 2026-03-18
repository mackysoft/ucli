using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    /// <summary> Provides in-memory state storage for request-id idempotency coordination flows. </summary>
    internal sealed class InMemoryExecuteRequestIdempotencyStore : IExecuteRequestIdempotencyStore
    {
        private readonly object syncRoot = new object();

        private readonly Dictionary<string, CompletedEntry> completedEntries = new Dictionary<string, CompletedEntry>(StringComparer.Ordinal);

        private readonly LinkedList<string> completedOrder = new LinkedList<string>();

        private readonly Dictionary<string, InFlightEntry> inFlightEntries = new Dictionary<string, InFlightEntry>(StringComparer.Ordinal);

        private readonly TimeSpan cacheTtl;

        private readonly int maxEntries;

        private readonly Func<DateTimeOffset> utcNowProvider;

        /// <summary> Initializes a new instance of the <see cref="InMemoryExecuteRequestIdempotencyStore" /> class. </summary>
        /// <param name="cacheTtl"> The cache TTL duration. Must be greater than <see cref="TimeSpan.Zero" />. </param>
        /// <param name="maxEntries"> The maximum number of completed entries retained in memory. Must be greater than zero. </param>
        /// <param name="utcNowProvider"> The UTC clock provider. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="cacheTtl" /> or <paramref name="maxEntries" /> is invalid. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="utcNowProvider" /> is <see langword="null" />. </exception>
        public InMemoryExecuteRequestIdempotencyStore (
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

        /// <summary> Acquires one idempotency decision for an incoming request-id and fingerprint. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <param name="requestFingerprint"> The deterministic request fingerprint. </param>
        /// <returns> The idempotency decision. </returns>
        public ExecuteRequestIdempotencyStoreDecision Acquire (
            string requestId,
            string requestFingerprint)
        {
            var nowUtc = utcNowProvider();

            lock (syncRoot)
            {
                EvictExpiredEntries(nowUtc);

                if (completedEntries.TryGetValue(requestId, out var completedEntry))
                {
                    if (string.Equals(completedEntry.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                    {
                        return ExecuteRequestIdempotencyStoreDecision.ReplayCompleted(completedEntry.Response);
                    }

                    return ExecuteRequestIdempotencyStoreDecision.Conflict();
                }

                if (inFlightEntries.TryGetValue(requestId, out var inFlightEntry))
                {
                    if (!string.Equals(inFlightEntry.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                    {
                        return ExecuteRequestIdempotencyStoreDecision.Conflict();
                    }

                    return ExecuteRequestIdempotencyStoreDecision.WaitInFlight(inFlightEntry.CompletionSource.Task);
                }

                var ownerCompletionSource = new TaskCompletionSource<IpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                inFlightEntries[requestId] = new InFlightEntry(requestFingerprint, ownerCompletionSource);
                return ExecuteRequestIdempotencyStoreDecision.ExecuteOwner();
            }
        }

        /// <summary> Completes one owner execution successfully and publishes the response to shared waiters. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <param name="requestFingerprint"> The deterministic request fingerprint. </param>
        /// <param name="response"> The completed response envelope. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="response" /> is <see langword="null" />. </exception>
        public void CompleteSuccess (
            string requestId,
            string requestFingerprint,
            IpcResponse response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            var createdAtUtc = utcNowProvider();
            lock (syncRoot)
            {
                if (!inFlightEntries.TryGetValue(requestId, out var inFlightEntry))
                {
                    return;
                }

                inFlightEntries.Remove(requestId);
                SaveCompletedEntry(requestId, requestFingerprint, response, createdAtUtc);
                inFlightEntry.CompletionSource.TrySetResult(response);
            }
        }

        /// <summary> Completes one owner execution with cancellation and notifies shared waiters. </summary>
        /// <param name="requestId"> The request identifier. </param>
        public void CompleteCanceled (string requestId)
        {
            lock (syncRoot)
            {
                if (!inFlightEntries.TryGetValue(requestId, out var inFlightEntry))
                {
                    return;
                }

                inFlightEntries.Remove(requestId);
                inFlightEntry.CompletionSource.TrySetCanceled();
            }
        }

        /// <summary> Completes one owner execution with failure and notifies shared waiters. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <param name="exception"> The execution failure exception. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
        public void CompleteFailed (
            string requestId,
            Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            lock (syncRoot)
            {
                if (!inFlightEntries.TryGetValue(requestId, out var inFlightEntry))
                {
                    return;
                }

                inFlightEntries.Remove(requestId);
                inFlightEntry.CompletionSource.TrySetException(exception);
            }
        }

        /// <summary> Saves one completed response entry and applies overflow eviction. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <param name="requestFingerprint"> The request fingerprint. </param>
        /// <param name="response"> The completed response. </param>
        /// <param name="createdAtUtc"> The completion timestamp in UTC. </param>
        private void SaveCompletedEntry (
            string requestId,
            string requestFingerprint,
            IpcResponse response,
            DateTimeOffset createdAtUtc)
        {
            if (completedEntries.TryGetValue(requestId, out var existingEntry))
            {
                completedOrder.Remove(existingEntry.OrderNode);
            }

            var orderNode = completedOrder.AddLast(requestId);
            completedEntries[requestId] = new CompletedEntry(
                RequestFingerprint: requestFingerprint,
                Response: response,
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
        /// <param name="RequestFingerprint"> The fingerprint used by the owner request. </param>
        /// <param name="CompletionSource"> The shared completion source for waiters. </param>
        private sealed record InFlightEntry (
            string RequestFingerprint,
            TaskCompletionSource<IpcResponse> CompletionSource);

        /// <summary> Represents one completed response cache entry keyed by request-id. </summary>
        /// <param name="RequestFingerprint"> The fingerprint used by the completed request. </param>
        /// <param name="Response"> The completed response envelope. </param>
        /// <param name="ExpiresAtUtc"> The expiration timestamp in UTC. </param>
        /// <param name="OrderNode"> The insertion-order linked-list node. </param>
        private sealed record CompletedEntry (
            string RequestFingerprint,
            IpcResponse Response,
            DateTimeOffset ExpiresAtUtc,
            LinkedListNode<string> OrderNode);
    }
}