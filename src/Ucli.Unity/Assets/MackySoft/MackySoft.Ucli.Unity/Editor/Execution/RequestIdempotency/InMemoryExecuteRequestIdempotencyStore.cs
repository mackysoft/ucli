using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    /// <summary> Provides in-memory state storage for request-id idempotency coordination flows. </summary>
    internal sealed class InMemoryExecuteRequestIdempotencyStore : IExecuteRequestIdempotencyStore
    {
        private readonly object syncRoot = new object();

        private readonly Dictionary<Guid, CompletedEntry> completedEntries = new Dictionary<Guid, CompletedEntry>();

        private readonly LinkedList<Guid> completedOrder = new LinkedList<Guid>();

        private readonly Dictionary<Guid, InFlightEntry> inFlightEntries = new Dictionary<Guid, InFlightEntry>();

        private readonly TimeSpan cacheTtl;

        private readonly int maxEntries;

        private readonly IMonotonicClock monotonicClock;

        /// <summary> Initializes a new instance of the <see cref="InMemoryExecuteRequestIdempotencyStore" /> class. </summary>
        /// <param name="cacheTtl"> The cache TTL duration. Must be greater than <see cref="TimeSpan.Zero" />. </param>
        /// <param name="maxEntries"> The maximum number of completed entries retained in memory. Must be greater than zero. </param>
        /// <param name="monotonicClock"> The monotonic process-time source. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="cacheTtl" /> or <paramref name="maxEntries" /> is invalid. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="monotonicClock" /> is <see langword="null" />. </exception>
        public InMemoryExecuteRequestIdempotencyStore (
            TimeSpan cacheTtl,
            int maxEntries,
            IMonotonicClock monotonicClock)
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
            this.monotonicClock = monotonicClock ?? throw new ArgumentNullException(nameof(monotonicClock));
        }

        /// <summary> Acquires one idempotency decision for an incoming request-id and fingerprint. </summary>
        /// <param name="requestId"> The request identifier. </param>
        /// <param name="requestFingerprint"> The deterministic request fingerprint. </param>
        /// <returns> The idempotency decision. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestFingerprint" /> is <see langword="null" />. </exception>
        public ExecuteRequestIdempotencyStoreDecision Acquire (
            Guid requestId,
            Sha256Digest requestFingerprint)
        {
            if (requestFingerprint == null)
            {
                throw new ArgumentNullException(nameof(requestFingerprint));
            }

            lock (syncRoot)
            {
                var monotonicNow = monotonicClock.Elapsed;
                EvictExpiredEntries(monotonicNow);

                if (completedEntries.TryGetValue(requestId, out var completedEntry))
                {
                    if (completedEntry.RequestFingerprint == requestFingerprint)
                    {
                        return ExecuteRequestIdempotencyStoreDecision.ReplayCompleted(completedEntry.Response);
                    }

                    return ExecuteRequestIdempotencyStoreDecision.Conflict();
                }

                if (inFlightEntries.TryGetValue(requestId, out var inFlightEntry))
                {
                    if (inFlightEntry.RequestFingerprint != requestFingerprint)
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
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestFingerprint" /> or <paramref name="response" /> is <see langword="null" />. </exception>
        public void CompleteSuccess (
            Guid requestId,
            Sha256Digest requestFingerprint,
            IpcResponse response)
        {
            if (requestFingerprint == null)
            {
                throw new ArgumentNullException(nameof(requestFingerprint));
            }

            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            lock (syncRoot)
            {
                if (!inFlightEntries.TryGetValue(requestId, out var inFlightEntry))
                {
                    return;
                }

                inFlightEntries.Remove(requestId);
                SaveCompletedEntry(requestId, requestFingerprint, response, monotonicClock.Elapsed);
                inFlightEntry.CompletionSource.TrySetResult(response);
            }
        }

        /// <summary> Completes one owner execution with cancellation and notifies shared waiters. </summary>
        /// <param name="requestId"> The request identifier. </param>
        public void CompleteCanceled (Guid requestId)
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
            Guid requestId,
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
        /// <param name="completedAtMonotonicTime"> The monotonic completion time. </param>
        private void SaveCompletedEntry (
            Guid requestId,
            Sha256Digest requestFingerprint,
            IpcResponse response,
            TimeSpan completedAtMonotonicTime)
        {
            if (completedEntries.TryGetValue(requestId, out var existingEntry))
            {
                completedOrder.Remove(existingEntry.OrderNode);
            }

            var orderNode = completedOrder.AddLast(requestId);
            completedEntries[requestId] = new CompletedEntry(
                RequestFingerprint: requestFingerprint,
                Response: response,
                CompletedAtMonotonicTime: completedAtMonotonicTime,
                OrderNode: orderNode);
            EvictOverflowEntries();
        }

        /// <summary> Evicts expired completed entries in chronological insertion order. </summary>
        /// <param name="monotonicNow"> The current monotonic process time. </param>
        private void EvictExpiredEntries (TimeSpan monotonicNow)
        {
            while (completedOrder.First != null)
            {
                var requestId = completedOrder.First.Value;
                if (!completedEntries.TryGetValue(requestId, out var entry))
                {
                    completedOrder.RemoveFirst();
                    continue;
                }

                if (monotonicNow - entry.CompletedAtMonotonicTime < cacheTtl)
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
            Sha256Digest RequestFingerprint,
            TaskCompletionSource<IpcResponse> CompletionSource);

        /// <summary> Represents one completed response cache entry keyed by request-id. </summary>
        /// <param name="RequestFingerprint"> The fingerprint used by the completed request. </param>
        /// <param name="Response"> The completed response envelope. </param>
        /// <param name="CompletedAtMonotonicTime"> The monotonic process time when the response completed. </param>
        /// <param name="OrderNode"> The insertion-order linked-list node. </param>
        private sealed record CompletedEntry (
            Sha256Digest RequestFingerprint,
            IpcResponse Response,
            TimeSpan CompletedAtMonotonicTime,
            LinkedListNode<Guid> OrderNode);
    }
}
