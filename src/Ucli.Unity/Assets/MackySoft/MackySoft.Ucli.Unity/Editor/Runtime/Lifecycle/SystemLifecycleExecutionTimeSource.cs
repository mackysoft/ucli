using System;
using System.Threading;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Supplies Lifecycle Execution time from the system UTC clock. </summary>
    internal sealed class SystemLifecycleExecutionTimeSource : ILifecycleExecutionTimeSource
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public ILifecycleExecutionDeadline CreateDeadlineNotification (
            DateTimeOffset deadlineUtc)
        {
            if (deadlineUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Lifecycle Execution deadline must use the UTC offset.",
                    nameof(deadlineUtc));
            }

            var source = new CancellationTokenSource();
            var remaining = deadlineUtc - UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                source.Cancel();
            }
            else
            {
                source.CancelAfter(remaining);
            }

            return new SystemLifecycleExecutionDeadline(source);
        }

        private sealed class SystemLifecycleExecutionDeadline :
            ILifecycleExecutionDeadline
        {
            private readonly CancellationTokenSource source;

            public SystemLifecycleExecutionDeadline (CancellationTokenSource source)
            {
                this.source = source ?? throw new ArgumentNullException(nameof(source));
            }

            public CancellationToken Token => source.Token;

            public bool IsCancellationRequested => source.IsCancellationRequested;

            public void Dispose ()
            {
                source.Dispose();
            }
        }
    }
}
