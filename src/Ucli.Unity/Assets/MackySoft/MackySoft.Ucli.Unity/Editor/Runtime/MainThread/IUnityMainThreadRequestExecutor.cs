using System;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Defines the shared safety fence applied after Unity mutation cancellation. </summary>
    internal static class UnityMutationCancellationPolicy
    {
        private static readonly TimeSpan QuiescenceGrace = TimeSpan.FromMilliseconds(100);

        /// <summary> Waits for an operation to reach a terminal state within the shared cancellation grace. </summary>
        /// <param name="terminalTask"> The task that completes when the operation can no longer mutate Unity state. </param>
        /// <returns> <see langword="true" /> when the operation reached its terminal state within the grace. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="terminalTask" /> is <see langword="null" />. </exception>
        internal static async Task<bool> WaitForQuiescenceAsync (Task terminalTask)
        {
            if (terminalTask == null)
            {
                throw new ArgumentNullException(nameof(terminalTask));
            }

            if (terminalTask.IsCompleted)
            {
                return true;
            }

            await Task.WhenAny(terminalTask, Task.Delay(QuiescenceGrace)).ConfigureAwait(false);
            return terminalTask.IsCompleted;
        }
    }

    /// <summary> Executes asynchronous work items on Unity main thread context. </summary>
    internal interface IUnityMainThreadRequestExecutor
    {
        /// <summary> Executes one asynchronous work item on Unity main thread. </summary>
        /// <typeparam name="T"> The work-item result type. </typeparam>
        /// <param name="workItem"> The asynchronous work item to execute. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by connection handling. </param>
        /// <returns> The work-item result. </returns>
        Task<T> ExecuteAsync<T> (
            Func<Task<T>> workItem,
            CancellationToken cancellationToken = default);
    }

    /// <summary> Executes control-plane IPC work on the Unity main-thread context. </summary>
    internal interface IUnityControlPlaneRequestExecutor
    {
        /// <summary> Executes one control-plane work item on the Unity main thread. </summary>
        Task<T> ExecuteAsync<T> (
            Func<Task<T>> workItem,
            CancellationToken cancellationToken);
    }

    /// <summary> Exposes whether the exclusive Unity mutation lane is occupied. </summary>
    internal interface IUnityMutationExecutionState
    {
        /// <summary> Gets a value indicating whether one mutation is running or queued. </summary>
        bool IsBusy { get; }
    }

    /// <summary> Controls mutation admission when an in-flight operation cannot be proven quiescent. </summary>
    internal interface IUnityMutationLaneControl : IUnityMutationExecutionState
    {
        /// <summary> Gets a value indicating whether mutation safety became indeterminate. </summary>
        bool IsPoisoned { get; }

        /// <summary> Permanently rejects mutation work for the current host generation. </summary>
        /// <param name="reason"> The operator-facing reason that requires an Editor restart. </param>
        void Poison (string reason);

        /// <summary> Atomically seals new admission only when no mutation is running or queued. </summary>
        /// <param name="admissionSeal"> The seal that restores admission when disposed. </param>
        /// <returns> <see langword="true" /> when admission was sealed while idle. </returns>
        bool TrySealAdmissionWhenIdle (out IDisposable admissionSeal);
    }
}
