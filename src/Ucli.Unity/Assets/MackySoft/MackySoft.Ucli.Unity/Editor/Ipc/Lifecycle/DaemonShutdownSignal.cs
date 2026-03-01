using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements in-process shutdown signal coordination for daemon bootstrap lifecycle. </summary>
    internal sealed class DaemonShutdownSignal : IDaemonShutdownSignal
    {
        private readonly TaskCompletionSource<bool> signalSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary> Signals that daemon shutdown has been requested. </summary>
        public void Signal ()
        {
            signalSource.TrySetResult(true);
        }

        /// <summary> Waits until daemon shutdown is requested. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes when shutdown is requested. </returns>
        public async Task Wait (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!cancellationToken.CanBeCanceled)
            {
                await signalSource.Task;
                return;
            }

            var cancellationSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationRegistration = cancellationToken.Register(static state =>
            {
                var completionSource = (TaskCompletionSource<bool>)state!;
                completionSource.TrySetResult(true);
            }, cancellationSource);

            var completedTask = await Task.WhenAny(signalSource.Task, cancellationSource.Task);
            if (!ReferenceEquals(completedTask, signalSource.Task))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            await signalSource.Task;
        }
    }
}
