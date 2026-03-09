using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Coordinates completion of the single request handled by Unity oneshot mode. </summary>
    internal sealed class OneshotRequestCompletionSignal
    {
        private readonly TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary> Signals that the oneshot request-response exchange has completed. </summary>
        internal void Signal ()
        {
            completionSource.TrySetResult(true);
        }

        /// <summary> Waits until the oneshot request-response exchange completes. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
        /// <returns> A task that completes when request handling finishes. </returns>
        internal Task Wait (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!cancellationToken.CanBeCanceled)
            {
                return completionSource.Task;
            }

            if (completionSource.Task.IsCompleted)
            {
                return completionSource.Task;
            }

            var waitCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationRegistration = cancellationToken.Register(() => waitCompletionSource.TrySetCanceled(cancellationToken));
            completionSource.Task.ContinueWith(
                completedTask =>
                {
                    cancellationRegistration.Dispose();
                    if (completedTask.IsCanceled)
                    {
                        waitCompletionSource.TrySetCanceled();
                        return;
                    }

                    if (completedTask.IsFaulted)
                    {
                        waitCompletionSource.TrySetException(completedTask.Exception.InnerExceptions);
                        return;
                    }

                    waitCompletionSource.TrySetResult(true);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return waitCompletionSource.Task;
        }
    }
}
