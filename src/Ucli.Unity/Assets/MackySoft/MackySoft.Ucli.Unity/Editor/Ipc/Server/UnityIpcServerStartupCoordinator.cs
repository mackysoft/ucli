using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Coordinates IPC server startup completion and delayed startup-failure propagation. </summary>
    internal sealed class UnityIpcServerStartupCoordinator
    {
        private static readonly TimeSpan StartupCompletionRaceGracePeriod = TimeSpan.FromMilliseconds(100);

        private readonly object syncRoot = new object();

        private readonly TaskCompletionSource<bool> startupCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private bool listenerStarted;

        private bool listenerLifetimeTracked;

        /// <summary> Records that the transport listener began accepting connections. </summary>
        public void SignalListenerStarted ()
        {
            lock (syncRoot)
            {
                listenerStarted = true;
                TryCompleteWithoutLock();
            }
        }

        /// <summary> Records that the listener lifetime task is available to endpoint publication checks. </summary>
        public void MarkListenerLifetimeTracked ()
        {
            lock (syncRoot)
            {
                listenerLifetimeTracked = true;
                TryCompleteWithoutLock();
            }
        }

        /// <summary> Marks startup as canceled while shutdown is in progress. </summary>
        public void Cancel ()
        {
            startupCompletionSource.TrySetCanceled();
        }

        /// <summary> Marks startup as failed by one listener exception. </summary>
        /// <param name="exception"> The startup failure exception. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
        public void Fail (Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            startupCompletionSource.TrySetException(exception);
        }

        /// <summary> Waits until startup succeeds, fails, or caller cancellation is requested. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by start operation. </param>
        /// <returns> A task that completes when startup result is determined. </returns>
        /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled. </exception>
        public async Task WaitAsync (
            CancellationToken cancellationToken)
        {
            var startupTask = startupCompletionSource.Task;
            await CancellationGracePeriodAwaiter.WaitAsync(startupTask, cancellationToken, StartupCompletionRaceGracePeriod);
        }

        private void TryCompleteWithoutLock ()
        {
            if (listenerStarted && listenerLifetimeTracked)
            {
                startupCompletionSource.TrySetResult(true);
            }
        }
    }
}
