using System;
using System.Threading;
using System.Threading.Tasks;

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
            cancellationToken.ThrowIfCancellationRequested();

            var startupTask = startupCompletionSource.Task;
            if (!cancellationToken.CanBeCanceled)
            {
                await startupTask;
                return;
            }

            var cancellationSignalTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationRegistration = cancellationToken.Register(static state =>
            {
                var taskSource = (TaskCompletionSource<bool>)state;
                taskSource.TrySetResult(true);
            }, cancellationSignalTaskSource);

            if (startupTask.IsCompleted)
            {
                await startupTask;
                return;
            }

            Task completedTask = await Task.WhenAny(startupTask, cancellationSignalTaskSource.Task);
            if (!ReferenceEquals(completedTask, startupTask) && !startupTask.IsCompleted)
            {
                // NOTE:
                // Caller cancellation can race with startup completion. Wait one bounded wall-clock grace period so
                // completion queued on another context can still win without relying on repeated Task.Yield scheduling.
                completedTask = await Task.WhenAny(startupTask, Task.Delay(StartupCompletionRaceGracePeriod));
                if (!ReferenceEquals(completedTask, startupTask) && !startupTask.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            await startupTask;
        }
    }
}
