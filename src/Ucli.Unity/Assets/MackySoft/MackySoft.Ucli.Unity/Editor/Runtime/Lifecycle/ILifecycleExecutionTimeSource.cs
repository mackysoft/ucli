using System;
using System.Threading;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary>
    /// Supplies the UTC observations and deadline notifications for durable Lifecycle Executions.
    /// </summary>
    internal interface ILifecycleExecutionTimeSource
    {
        /// <summary> Gets the current UTC observation. </summary>
        DateTimeOffset UtcNow { get; }

        /// <summary> Creates one disposable notification that signals when the UTC deadline is reached. </summary>
        ILifecycleExecutionDeadline CreateDeadlineNotification (DateTimeOffset deadlineUtc);
    }

    /// <summary> Represents one disposable Lifecycle Execution deadline notification. </summary>
    internal interface ILifecycleExecutionDeadline : IDisposable
    {
        /// <summary> Gets the cancellation token that signals deadline arrival. </summary>
        CancellationToken Token { get; }

        /// <summary> Gets whether the deadline has arrived. </summary>
        bool IsCancellationRequested { get; }
    }
}
