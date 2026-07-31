using System;
using System.Threading;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary>
    /// Separates one durable Lifecycle Execution deadline from the current caller's wait lifetime.
    /// </summary>
    internal sealed class LifecycleExecutionDeadlineScope : IDisposable
    {
        private readonly CancellationTokenSource deadlineCancellationTokenSource;
        private readonly CancellationTokenSource executionCancellationTokenSource;

        public LifecycleExecutionDeadlineScope (
            DateTimeOffset deadlineUtc,
            CancellationToken continuationCancellationToken)
        {
            if (deadlineUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Lifecycle Execution deadline must use the UTC offset.",
                    nameof(deadlineUtc));
            }

            deadlineCancellationTokenSource = new CancellationTokenSource();
            var remaining = deadlineUtc - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                deadlineCancellationTokenSource.Cancel();
            }
            else
            {
                deadlineCancellationTokenSource.CancelAfter(remaining);
            }

            executionCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(
                    deadlineCancellationTokenSource.Token,
                    continuationCancellationToken);
        }

        public CancellationToken Token =>
            executionCancellationTokenSource.Token;

        public bool IsDeadlineExceeded =>
            deadlineCancellationTokenSource.IsCancellationRequested;

        public void Dispose ()
        {
            executionCancellationTokenSource.Dispose();
            deadlineCancellationTokenSource.Dispose();
        }
    }
}
