using System;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Keeps durable Lifecycle Execution acceptance independent from daemon and user-owned GUI process lifetime.
    /// </summary>
    internal sealed class NoOpLifecycleExecutionHostLifetimeObserver :
        ILifecycleExecutionHostLifetimeObserver
    {
        public static NoOpLifecycleExecutionHostLifetimeObserver Instance { get; } = new();

        private NoOpLifecycleExecutionHostLifetimeObserver ()
        {
        }

        /// <inheritdoc />
        public void OnStartAccepted (DateTimeOffset deadlineUtc)
        {
        }
    }
}
