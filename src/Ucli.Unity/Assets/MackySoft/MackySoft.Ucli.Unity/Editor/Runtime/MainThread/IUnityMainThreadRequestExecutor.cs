using System;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Defines the shared safety fence applied after Unity mutation cancellation. </summary>
    internal static class UnityMutationCancellationPolicy
    {
        internal static readonly TimeSpan QuiescenceGrace = TimeSpan.FromMilliseconds(100);

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

    /// <summary> Publishes the durability barrier immediately before one mutation-lane request begins. </summary>
    internal interface IUnityMutationRequestExecutionStartSource
    {
        /// <summary>
        /// Occurs on the Unity main thread after the lane becomes busy. The request delegate is not invoked until
        /// every handler completes successfully.
        /// </summary>
        event Func<CancellationToken, Task>? RequestExecutionStarting;
    }

    /// <summary> Exposes whether the exclusive Unity mutation lane is occupied. </summary>
    internal interface IUnityMutationExecutionState
    {
        /// <summary> Gets a value indicating whether this generation is executing work or rejecting new admission. </summary>
        bool IsBusy { get; }

        /// <summary> Gets a value indicating whether work owned by this host generation has not terminated. </summary>
        bool HasUnfinishedWork { get; }
    }

    /// <summary> Represents the safety lifetime of one Unity-state mutation. </summary>
    internal interface IUnityMutationActivity
    {
        /// <summary> Marks the mutation as unable to perform any further Unity-state changes. </summary>
        void Complete ();
    }

    /// <summary> Controls explicit Unity-mutation boundaries and host-generation retirement. </summary>
    internal interface IUnityMutationLaneControl : IUnityMutationExecutionState
    {
        /// <summary> Gets a value indicating whether the current host generation rejects further work. </summary>
        bool IsQuarantined { get; }

        /// <summary> Starts one Unity-state mutation in the currently executing request. </summary>
        /// <returns> The activity that must be completed only after the mutation can no longer change Unity state. </returns>
        /// <exception cref="InvalidOperationException"> Thrown when there is no active request or that request already started a mutation. </exception>
        /// <exception cref="OperationCanceledException"> Thrown when the active request was canceled before the mutation boundary. </exception>
        IUnityMutationActivity BeginMutation ();

        /// <summary> Rejects further work for this host generation until its unfinished mutation terminates. </summary>
        /// <param name="reason"> The operator-facing reason for retiring the current host generation. </param>
        /// <param name="mutationCompletion"> The task that completes when the unfinished mutation can no longer change Unity state. </param>
        /// <exception cref="InvalidOperationException"> Thrown when the current request has not started an unfinished Unity mutation. </exception>
        void Quarantine (string reason, Task mutationCompletion);

        /// <summary> Atomically seals admission when the generation is idle or already quarantined for retirement. </summary>
        /// <param name="admissionSeal"> The seal that restores admission when disposed. </param>
        /// <returns> <see langword="true" /> when the generation can begin retirement. </returns>
        bool TrySealAdmissionForRetirement (out IDisposable admissionSeal);

        /// <summary> Waits until all requests and mutation-safety dependencies owned by this generation terminate. </summary>
        Task WaitForRetirementAsync ();
    }
}
