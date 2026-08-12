using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Owns durable start matching, execution-deadline classification, and cancellation
    /// reclassification without selecting or running an action state machine.
    /// </summary>
    internal sealed class LifecycleExecutionAttemptBoundary
    {
        private readonly FileLifecycleExecutionStore executionStore;
        private readonly ILifecycleExecutionTimeSource timeSource;

        public LifecycleExecutionAttemptBoundary (
            FileLifecycleExecutionStore executionStore,
            ILifecycleExecutionTimeSource timeSource)
        {
            this.executionStore = executionStore
                ?? throw new ArgumentNullException(nameof(executionStore));
            this.timeSource = timeSource
                ?? throw new ArgumentNullException(nameof(timeSource));
        }

        /// <summary>
        /// Requires an exact durable Start Record, then returns the authoritative state that
        /// determines whether the selected action handler may continue.
        /// </summary>
        public async ValueTask<LifecycleExecutionAttemptResolution>
            ResolveInvocationAsync (
            LifecycleExecutionKind kind,
            LifecycleExecutionStartBinding requestedStart,
            CancellationToken cancellationToken)
        {
            if (requestedStart == null)
            {
                throw new ArgumentNullException(nameof(requestedStart));
            }

            var executionId = requestedStart.LifecycleExecutionRef.Id;
            var initial = await executionStore.ReadAsync(
                kind,
                executionId,
                cancellationToken);
            if (initial == null)
            {
                return new LifecycleExecutionAttemptResolution.Missing();
            }

            var initialMatch = LifecycleExecutionStartBindingMatcher.Match(
                requestedStart,
                initial.Start);
            if (initialMatch != LifecycleExecutionStartBindingMatch.Exact)
            {
                return new LifecycleExecutionAttemptResolution.BindingMismatch(
                    initialMatch);
            }

            // Re-read after matching so terminal publication that won concurrently with
            // invocation admission always bypasses provider work.
            return await ResolveAuthoritativeAsync(
                kind,
                executionId,
                requestedStart,
                cancellationToken);
        }

        /// <summary>
        /// Returns the authoritative deadline or terminal-publication state for a recovery that
        /// already carries the durable Start Record selected by common host admission.
        /// </summary>
        public ValueTask<LifecycleExecutionAttemptResolution>
            ResolveRecoveryAsync (
            LifecycleExecutionKind kind,
            Guid executionId,
            CancellationToken cancellationToken)
        {
            if (executionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Lifecycle Execution identifier must not be empty.",
                    nameof(executionId));
            }

            return ResolveAuthoritativeAsync(
                kind,
                executionId,
                requestedStart: null,
                cancellationToken);
        }

        /// <summary>
        /// Re-reads durable state only after the immutable execution deadline signals. A
        /// cancellation from any other lifetime is propagated unchanged.
        /// </summary>
        private ValueTask<LifecycleExecutionAttemptResolution>
            ReclassifyDeadlineCancellationAsync (
            LifecycleExecutionKind kind,
            LifecycleExecutionAttemptResolution.Open open,
            OperationCanceledException cancellation)
        {
            if (open == null)
            {
                throw new ArgumentNullException(nameof(open));
            }
            if (cancellation == null)
            {
                throw new ArgumentNullException(nameof(cancellation));
            }
            if (!open.IsDeadlineExceeded)
            {
                throw cancellation;
            }

            // A terminal publication that became authoritative while deadline cancellation was
            // being observed must win over a new deadline terminal candidate.
            return ResolveAfterDeadlineAsync(
                kind,
                open.Execution.CurrentReference.Id);
        }

        /// <summary>
        /// Observes an action-owned operation that has already been started with the immutable
        /// deadline token. Only deadline cancellation is reclassified; caller cancellation and
        /// all action failures retain their original meaning.
        /// </summary>
        public async ValueTask<LifecycleExecutionAttemptResolution>
            ObserveCompletionAsync<T> (
            LifecycleExecutionKind kind,
            LifecycleExecutionAttemptResolution.Open open,
            Task<T> actionOperation)
        {
            if (open == null)
            {
                throw new ArgumentNullException(nameof(open));
            }
            if (actionOperation == null)
            {
                throw new ArgumentNullException(nameof(actionOperation));
            }

            try
            {
                return new LifecycleExecutionAttemptResolution.Completed<T>(
                    await actionOperation);
            }
            catch (OperationCanceledException cancellation)
            {
                return await ReclassifyDeadlineCancellationAsync(
                    kind,
                    open,
                    cancellation);
            }
        }

        /// <summary>
        /// Observes an action-owned operation without a result value under the same immutable
        /// deadline-classification guarantee.
        /// </summary>
        public async ValueTask<LifecycleExecutionAttemptResolution>
            ObserveCompletionAsync (
            LifecycleExecutionKind kind,
            LifecycleExecutionAttemptResolution.Open open,
            Task actionOperation)
        {
            if (open == null)
            {
                throw new ArgumentNullException(nameof(open));
            }
            if (actionOperation == null)
            {
                throw new ArgumentNullException(nameof(actionOperation));
            }

            try
            {
                await actionOperation;
                return new LifecycleExecutionAttemptResolution.Completed();
            }
            catch (OperationCanceledException cancellation)
            {
                return await ReclassifyDeadlineCancellationAsync(
                    kind,
                    open,
                    cancellation);
            }
        }

        private async ValueTask<LifecycleExecutionAttemptResolution>
            ResolveAuthoritativeAsync (
            LifecycleExecutionKind kind,
            Guid executionId,
            LifecycleExecutionStartBinding requestedStart,
            CancellationToken cancellationToken)
        {
            var execution = await executionStore.ReadAsync(
                kind,
                executionId,
                cancellationToken);
            if (execution == null)
            {
                return new LifecycleExecutionAttemptResolution.Missing();
            }

            if (requestedStart != null)
            {
                var match = LifecycleExecutionStartBindingMatcher.Match(
                    requestedStart,
                    execution.Start);
                if (match != LifecycleExecutionStartBindingMatch.Exact)
                {
                    return new LifecycleExecutionAttemptResolution
                        .BindingMismatch(match);
                }
            }

            if (execution.IsTerminal || execution.IsPublishing)
            {
                return new LifecycleExecutionAttemptResolution
                    .TerminalOrPublishing(execution);
            }

            var open = new LifecycleExecutionAttemptResolution.Open(
                execution,
                timeSource);
            if (!open.IsDeadlineExceeded)
            {
                return open;
            }

            open.Dispose();
            return new LifecycleExecutionAttemptResolution
                .DeadlineExceeded(execution);
        }

        private async ValueTask<LifecycleExecutionAttemptResolution>
            ResolveAfterDeadlineAsync (
            LifecycleExecutionKind kind,
            Guid executionId)
        {
            var execution = await executionStore.ReadAsync(
                kind,
                executionId,
                CancellationToken.None);
            if (execution == null)
            {
                return new LifecycleExecutionAttemptResolution.Missing();
            }
            if (execution.IsTerminal || execution.IsPublishing)
            {
                return new LifecycleExecutionAttemptResolution
                    .TerminalOrPublishing(execution);
            }

            return new LifecycleExecutionAttemptResolution
                .DeadlineExceeded(execution);
        }
    }
}
