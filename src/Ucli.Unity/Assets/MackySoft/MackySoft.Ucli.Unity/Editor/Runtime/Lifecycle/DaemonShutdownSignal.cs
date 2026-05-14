using System;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Implements in-process shutdown signal coordination for daemon bootstrap lifecycle. </summary>
    internal sealed class DaemonShutdownSignal : IDaemonShutdownSignal
    {
        private static readonly TimeSpan ShutdownSignalRaceGracePeriod = TimeSpan.FromMilliseconds(10);

        private readonly TaskCompletionSource<bool> signalSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public bool IsSignaled => signalSource.Task.IsCompleted;

        /// <summary> Signals that daemon shutdown has been requested. </summary>
        public void Signal ()
        {
            signalSource.TrySetResult(true);
        }

        /// <summary> Waits until daemon shutdown is requested. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes when shutdown is requested. </returns>
        public async Task WaitAsync (CancellationToken cancellationToken = default)
        {
            var signalTask = signalSource.Task;
            await CancellationGracePeriodAwaiter.WaitAsync(signalTask, cancellationToken, ShutdownSignalRaceGracePeriod);
        }
    }
}
