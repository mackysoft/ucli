using System;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Waits for task completion while allowing caller cancellation to win after one bounded grace period. </summary>
    internal static class CancellationGracePeriodAwaiter
    {
        /// <summary> Awaits a task, but gives completion one bounded grace period to beat a racing cancellation request. </summary>
        /// <param name="task"> The task to await. </param>
        /// <param name="cancellationToken"> The caller cancellation token. </param>
        /// <param name="gracePeriod"> The bounded grace period used when completion races with cancellation. </param>
        public static async Task Wait (
            Task task,
            CancellationToken cancellationToken,
            TimeSpan gracePeriod)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (gracePeriod < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(gracePeriod));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!cancellationToken.CanBeCanceled)
            {
                await task;
                return;
            }

            var cancellationSignalSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationRegistration = cancellationToken.Register(static state =>
            {
                var taskSource = (TaskCompletionSource<bool>)state!;
                taskSource.TrySetResult(true);
            }, cancellationSignalSource);

            var completedTask = await Task.WhenAny(task, cancellationSignalSource.Task);
            if (!ReferenceEquals(completedTask, task) && !task.IsCompleted)
            {
                var graceCompletionTask = await Task.WhenAny(task, Task.Delay(gracePeriod));
                if (!ReferenceEquals(graceCompletionTask, task) && !task.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            await task;
        }
    }
}