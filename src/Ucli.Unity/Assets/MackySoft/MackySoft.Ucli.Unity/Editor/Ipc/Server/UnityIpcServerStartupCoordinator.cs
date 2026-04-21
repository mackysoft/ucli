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

        private readonly TaskCompletionSource<bool> startupCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary> Marks startup as completed after transport listener begins accept loop. </summary>
        public void Complete ()
        {
            startupCompletionSource.TrySetResult(true);
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

        /// <summary> Fails startup when listener loop exits unexpectedly before startup completion signal. </summary>
        /// <param name="isCancellationRequested"> Whether cancellation was already requested. </param>
        /// <param name="isRunning"> Whether server lifecycle is still marked as running. </param>
        public void FailOnUnexpectedExit (
            bool isCancellationRequested,
            bool isRunning)
        {
            if (startupCompletionSource.Task.IsCompleted || isCancellationRequested || !isRunning)
            {
                return;
            }

            startupCompletionSource.TrySetException(
                new InvalidOperationException("IPC server loop exited unexpectedly before startup completed."));
        }

        /// <summary> Waits until startup succeeds, fails, or caller cancellation is requested. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by start operation. </param>
        /// <returns> A task that completes when startup result is determined. </returns>
        /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled. </exception>
        public async Task Wait (
            CancellationToken cancellationToken)
        {
            var startupTask = startupCompletionSource.Task;
            await CancellationGracePeriodAwaiter.Wait(startupTask, cancellationToken, StartupCompletionRaceGracePeriod);
        }
    }
}
