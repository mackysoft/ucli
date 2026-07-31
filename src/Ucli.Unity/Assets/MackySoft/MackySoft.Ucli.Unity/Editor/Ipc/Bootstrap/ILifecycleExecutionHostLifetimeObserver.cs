using System;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Observes durable Lifecycle Execution acceptance at the boundary that owns the current Unity host.
    /// </summary>
    internal interface ILifecycleExecutionHostLifetimeObserver
    {
        /// <summary>
        /// Observes that one Lifecycle Execution start record is durable before its response is delivered.
        /// </summary>
        /// <param name="deadlineUtc"> The immutable execution deadline stored by the provider. </param>
        void OnStartAccepted (DateTimeOffset deadlineUtc);
    }
}
