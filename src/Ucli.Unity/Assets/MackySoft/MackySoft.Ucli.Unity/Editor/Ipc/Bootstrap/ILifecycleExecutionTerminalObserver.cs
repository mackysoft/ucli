using System;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Observes a durable terminal Lifecycle Execution at the boundary that owns the current host
    /// process lifetime.
    /// </summary>
    internal interface ILifecycleExecutionTerminalObserver
    {
        /// <summary>
        /// Observes that the authoritative execution store now contains a terminal reference.
        /// </summary>
        void OnTerminal (LifecycleExecutionKind kind, Guid executionId);
    }

    /// <summary> Leaves daemon and user-owned GUI process lifetime independent from execution completion. </summary>
    internal sealed class NoOpLifecycleExecutionTerminalObserver :
        ILifecycleExecutionTerminalObserver
    {
        public static NoOpLifecycleExecutionTerminalObserver Instance { get; } =
            new NoOpLifecycleExecutionTerminalObserver();

        private NoOpLifecycleExecutionTerminalObserver ()
        {
        }

        /// <inheritdoc />
        public void OnTerminal (LifecycleExecutionKind kind, Guid executionId)
        {
        }
    }
}
