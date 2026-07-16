using System;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Bounds foreground shutdown while preserving the actual lifetime of work owned by one host generation. </summary>
    internal static class UnityHostGenerationRetirementPolicy
    {
        /// <summary> Gets the maximum time shutdown remains in the foreground waiting for admitted work to terminate. </summary>
        internal static readonly TimeSpan ForegroundDeadline = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Waits for one generation retirement task up to <see cref="ForegroundDeadline" /> without canceling or
        /// forgetting the underlying work.
        /// </summary>
        /// <param name="retirementTask"> The task that represents actual termination of all admitted work. </param>
        /// <returns>
        /// <see langword="true" /> when retirement completed within the foreground deadline; otherwise
        /// <see langword="false" />. A fault completed within the deadline is propagated.
        /// </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="retirementTask" /> is <see langword="null" />. </exception>
        internal static async Task<bool> WaitWithinForegroundDeadlineAsync (Task retirementTask)
        {
            if (retirementTask == null)
            {
                throw new ArgumentNullException(nameof(retirementTask));
            }

            if (retirementTask.IsCompleted)
            {
                await retirementTask.ConfigureAwait(false);
                return true;
            }

            var deadlineTask = Task.Delay(ForegroundDeadline);
            var completedTask = await Task.WhenAny(retirementTask, deadlineTask).ConfigureAwait(false);
            if (!ReferenceEquals(completedTask, retirementTask) && !retirementTask.IsCompleted)
            {
                ObserveFault(retirementTask);
                return false;
            }

            await retirementTask.ConfigureAwait(false);
            return true;
        }

        private static void ObserveFault (Task task)
        {
            _ = task.ContinueWith(
                static completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }
}
