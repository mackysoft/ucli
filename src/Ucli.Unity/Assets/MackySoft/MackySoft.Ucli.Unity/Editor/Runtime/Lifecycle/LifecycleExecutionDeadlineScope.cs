using System;
using System.Threading;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary>
    /// Separates one durable Lifecycle Execution deadline from the current caller's wait lifetime.
    /// </summary>
    internal sealed class LifecycleExecutionDeadlineScope : IDisposable
    {
        private readonly ILifecycleExecutionDeadline deadlineNotification;
        private readonly CancellationTokenSource executionCancellationTokenSource;

        public LifecycleExecutionDeadlineScope (
            DateTimeOffset deadlineUtc,
            ILifecycleExecutionTimeSource timeSource,
            CancellationToken continuationCancellationToken)
        {
            if (timeSource == null)
            {
                throw new ArgumentNullException(nameof(timeSource));
            }

            deadlineNotification = timeSource.CreateDeadlineNotification(deadlineUtc);

            executionCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(
                    deadlineNotification.Token,
                    continuationCancellationToken);
        }

        public CancellationToken Token =>
            executionCancellationTokenSource.Token;

        public bool IsDeadlineExceeded =>
            deadlineNotification.IsCancellationRequested;

        public void Dispose ()
        {
            executionCancellationTokenSource.Dispose();
            deadlineNotification.Dispose();
        }
    }
}
