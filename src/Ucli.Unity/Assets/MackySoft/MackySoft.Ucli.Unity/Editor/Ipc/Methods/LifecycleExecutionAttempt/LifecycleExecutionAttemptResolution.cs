using System;
using System.Threading;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Carries one exclusive classification of the authoritative durable state before an
    /// action-owned Lifecycle Execution attempt continues.
    /// </summary>
    internal abstract class LifecycleExecutionAttemptResolution
    {
        private LifecycleExecutionAttemptResolution ()
        {
        }

        /// <summary> Identifies that no durable execution exists for the requested identity. </summary>
        internal sealed class Missing : LifecycleExecutionAttemptResolution
        {
        }

        /// <summary> Carries the durable start fact that rejected an invocation binding. </summary>
        internal sealed class BindingMismatch : LifecycleExecutionAttemptResolution
        {
            public BindingMismatch (LifecycleExecutionStartBindingMatch match)
            {
                if (match == LifecycleExecutionStartBindingMatch.Exact)
                {
                    throw new ArgumentException(
                        "An exact Lifecycle Execution start binding is not a mismatch.",
                        nameof(match));
                }

                Match = match;
            }

            public LifecycleExecutionStartBindingMatch Match { get; }
        }

        /// <summary> Carries an open execution whose immutable deadline has elapsed. </summary>
        internal sealed class DeadlineExceeded : LifecycleExecutionAttemptResolution
        {
            public DeadlineExceeded (StoredLifecycleExecution execution)
            {
                Execution = RequireOpen(execution);
            }

            public StoredLifecycleExecution Execution { get; }
        }

        /// <summary>
        /// Carries an execution that must bypass action-provider work because terminal
        /// publication has started or completed.
        /// </summary>
        internal sealed class TerminalOrPublishing :
            LifecycleExecutionAttemptResolution
        {
            public TerminalOrPublishing (StoredLifecycleExecution execution)
            {
                if (execution == null)
                {
                    throw new ArgumentNullException(nameof(execution));
                }
                if (!execution.IsTerminal && !execution.IsPublishing)
                {
                    throw new ArgumentException(
                        "Lifecycle Execution must be terminal or publishing.",
                        nameof(execution));
                }

                Execution = execution;
            }

            public StoredLifecycleExecution Execution { get; }
        }

        /// <summary>
        /// Carries the action-owned result of one operation that completed before the durable
        /// execution deadline became authoritative.
        /// </summary>
        internal sealed class Completed : LifecycleExecutionAttemptResolution
        {
        }

        /// <summary> Carries an action-owned result fixed before the deadline. </summary>
        internal sealed class Completed<T> : LifecycleExecutionAttemptResolution
        {
            public Completed (T result)
            {
                Result = result;
            }

            public T Result { get; }
        }

        /// <summary>
        /// Carries an authoritative open execution and a cancellation lifetime controlled only
        /// by its immutable execution deadline.
        /// </summary>
        internal sealed class Open : LifecycleExecutionAttemptResolution, IDisposable
        {
            private readonly LifecycleExecutionDeadlineScope deadlineScope;

            public Open (StoredLifecycleExecution execution)
            {
                Execution = RequireOpen(execution);
                deadlineScope = new LifecycleExecutionDeadlineScope(
                    Execution.Start.DeadlineUtc,
                    CancellationToken.None);
            }

            public StoredLifecycleExecution Execution { get; }

            public CancellationToken DeadlineCancellationToken =>
                deadlineScope.Token;

            internal bool IsDeadlineExceeded =>
                deadlineScope.IsDeadlineExceeded;

            public void Dispose ()
            {
                deadlineScope.Dispose();
            }
        }

        private static StoredLifecycleExecution RequireOpen (
            StoredLifecycleExecution execution)
        {
            if (execution == null)
            {
                throw new ArgumentNullException(nameof(execution));
            }
            if (execution.IsTerminal || execution.IsPublishing)
            {
                throw new ArgumentException(
                    "Lifecycle Execution must be open.",
                    nameof(execution));
            }

            return execution;
        }
    }
}
