using System.Collections.Concurrent;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;

/// <summary>
/// Owns daemon mutations that outlive their caller deadline and serializes later work within the same project lane.
/// </summary>
internal sealed class DaemonCompensationOperationOwner
{
    private static readonly Task NeverCompletingTask = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously).Task;

    private readonly ConcurrentDictionary<OwnedOperationKey, OwnedCompensation> ownedCompensations = new();

    /// <summary>
    /// Transfers a physical project lifecycle lease to active deferred compensation for the same project.
    /// </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="lifecycleLease"> The acquired lifecycle lease to retain until compensation quiesces. </param>
    /// <returns>
    /// <see langword="true" /> when ownership transferred; <see langword="false" /> when no deferred compensation remains.
    /// </returns>
    public bool TryTransferLifecycleLease (
        ResolvedUnityProjectContext unityProject,
        IAsyncDisposable lifecycleLease)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(lifecycleLease);

        var operationKey = CreateOperationKey(
            unityProject,
            DaemonOperationLane.LifecycleCompensation);
        return ownedCompensations.TryGetValue(operationKey, out var ownedCompensation)
            && ownedCompensation.TryOwnLifecycleLease(lifecycleLease);
    }

    /// <summary> Waits until compensation already owned for the project has quiesced. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The caller deadline that bounds admission waiting. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <param name="timeoutMessage"> The structured timeout message. </param>
    /// <returns> <see langword="null" /> after quiescence; otherwise a timeout error. </returns>
    public async ValueTask<ExecutionError?> WaitForQuiescenceAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken,
        string timeoutMessage)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeoutMessage);
        cancellationToken.ThrowIfCancellationRequested();

        var operationKey = CreateOperationKey(
            unityProject,
            DaemonOperationLane.LifecycleCompensation);
        return await WaitForLaneQuiescenceAsync(
                operationKey,
                deadline,
                cancellationToken,
                timeoutMessage)
            .ConfigureAwait(false);
    }

    private async ValueTask<ExecutionError?> WaitForLaneQuiescenceAsync (
        OwnedOperationKey operationKey,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken,
        string timeoutMessage)
    {
        while (ownedCompensations.TryGetValue(operationKey, out var ownedCompensation))
        {
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return ExecutionError.Timeout(timeoutMessage);
            }

            var waitResult = await WaitForTaskAsync(
                    ownedCompensation.QuiescenceTask,
                    remainingTimeout,
                    deadline.Clock,
                    cancellationToken)
                .ConfigureAwait(false);
            if (waitResult == TaskWaitResult.CallerCanceled)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (waitResult == TaskWaitResult.TimedOut)
            {
                return ExecutionError.Timeout(timeoutMessage);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return null;
    }

    /// <summary>
    /// Runs one daemon-owned mutation in the foreground until completion or deadline, retaining ownership after timeout.
    /// </summary>
    /// <typeparam name="T"> The operation result type. </typeparam>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="lane"> The lane that defines which same-project mutations serialize with this operation. </param>
    /// <param name="deadline"> The single deadline shared by the owned operation. </param>
    /// <param name="cancellationToken">
    /// The token that cancels the caller's wait after the owned mutation has been admitted. The mutation continues with its owned token.
    /// </param>
    /// <param name="beforeTimeoutMessage"> The timeout message used when admission exhausts the deadline. </param>
    /// <param name="operationTimeoutMessage"> The timeout message used when the mutation exhausts the deadline. </param>
    /// <param name="operation"> The mutation to execute with its current remaining budget and owned token. </param>
    /// <returns> The completed value or a structured timeout error. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="lane" /> is unsupported. </exception>
    public async ValueTask<ExecutionDeadlineOperationResult<T>> ExecuteAsync<T> (
        ResolvedUnityProjectContext unityProject,
        DaemonOperationLane lane,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken,
        string beforeTimeoutMessage,
        string operationTimeoutMessage,
        Func<TimeSpan, CancellationToken, ValueTask<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(beforeTimeoutMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationTimeoutMessage);
        ArgumentNullException.ThrowIfNull(operation);

        var operationKey = CreateOperationKey(unityProject, lane);
        while (true)
        {
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ExecutionDeadlineOperationResult<T>.Failure(
                    ExecutionError.Timeout(beforeTimeoutMessage));
            }

            var ownedCompensation = new OwnedCompensation();
            if (!ownedCompensations.TryAdd(operationKey, ownedCompensation))
            {
                ownedCompensation.Dispose();
                var admissionError = await WaitForLaneQuiescenceAsync(
                        operationKey,
                        deadline,
                        cancellationToken,
                        beforeTimeoutMessage)
                    .ConfigureAwait(false);
                if (admissionError is not null)
                {
                    return ExecutionDeadlineOperationResult<T>.Failure(admissionError);
                }

                continue;
            }

            return await ExecuteOwnedAsync(
                    operationKey,
                    ownedCompensation,
                    deadline,
                    remainingTimeout,
                    cancellationToken,
                    operationTimeoutMessage,
                    operation)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<ExecutionDeadlineOperationResult<T>> ExecuteOwnedAsync<T> (
        OwnedOperationKey operationKey,
        OwnedCompensation ownedCompensation,
        ExecutionDeadline deadline,
        TimeSpan remainingTimeout,
        CancellationToken cancellationToken,
        string operationTimeoutMessage,
        Func<TimeSpan, CancellationToken, ValueTask<T>> operation)
    {
        var operationTask = Task.Run(
            async () => await operation(
                    remainingTimeout,
                    ownedCompensation.CancellationTokenSource.Token)
                .ConfigureAwait(false),
            CancellationToken.None);
        var waitResult = await WaitForTaskAsync(
                operationTask,
                remainingTimeout,
                deadline.Clock,
                cancellationToken)
            .ConfigureAwait(false);
        if (waitResult == TaskWaitResult.Completed)
        {
            try
            {
                var value = await operationTask.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return deadline.TryGetRemainingTimeout(out _)
                    ? ExecutionDeadlineOperationResult<T>.Success(value)
                    : ExecutionDeadlineOperationResult<T>.Failure(
                        ExecutionError.Timeout(operationTimeoutMessage));
            }
            finally
            {
                await ReleaseOwnershipAsync(operationKey, ownedCompensation).ConfigureAwait(false);
            }
        }

        var cancellationRequestTask = waitResult == TaskWaitResult.TimedOut
            ? ownedCompensation.CancellationTokenSource.CancelAsync()
            : RequestCancellationAtDeadlineAsync(
                operationTask,
                deadline,
                ownedCompensation.CancellationTokenSource);
        ownedCompensation.DeferredOwnershipTask = ObserveDeferredMutationAndReleaseAsync(
            operationKey,
            ownedCompensation,
            operationTask,
            cancellationRequestTask);

        cancellationToken.ThrowIfCancellationRequested();

        return ExecutionDeadlineOperationResult<T>.Failure(
            ExecutionError.Timeout(operationTimeoutMessage));
    }

    private async Task ObserveDeferredMutationAndReleaseAsync<T> (
        OwnedOperationKey operationKey,
        OwnedCompensation ownedCompensation,
        Task<T> operationTask,
        Task cancellationRequestTask)
    {
        try
        {
            try
            {
                _ = await operationTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The caller already received timeout or cancellation. Observation here prevents an unowned fault.
            }

            try
            {
                await cancellationRequestTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Cancellation callbacks are part of owned quiescence and are observed before ownership is released.
            }
        }
        finally
        {
            await ReleaseOwnershipAsync(operationKey, ownedCompensation).ConfigureAwait(false);
        }
    }

    private async ValueTask ReleaseOwnershipAsync (
        OwnedOperationKey operationKey,
        OwnedCompensation ownedCompensation)
    {
        var lifecycleLease = ownedCompensation.MarkReleasedAndTakeLifecycleLease();
        if (lifecycleLease is not null)
        {
            try
            {
                await lifecycleLease.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A lease that could not be released remains owned so no later lifecycle mutation is admitted.
                ownedCompensation.RetainFailedLifecycleLease(lifecycleLease);
                ownedCompensation.DisposeCancellationTokenSource();
                return;
            }
        }

        ownedCompensation.DisposeCancellationTokenSource();
        if (ownedCompensations.TryGetValue(operationKey, out var current)
            && ReferenceEquals(current, ownedCompensation))
        {
            _ = ownedCompensations.TryRemove(operationKey, out _);
        }

        ownedCompensation.SignalQuiescence();
    }

    private static async ValueTask<TaskWaitResult> WaitForTaskAsync (
        Task task,
        TimeSpan timeout,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource();
        var timeoutTask = Task.Delay(
            timeout,
            timeProvider,
            timeoutCancellationTokenSource.Token);
        var cancellationCompletionSource = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationRegistration = cancellationToken.CanBeCanceled
            ? cancellationToken.UnsafeRegister(
                static state => ((TaskCompletionSource)state!).TrySetResult(),
                cancellationCompletionSource)
            : default;
        var cancellationTask = cancellationToken.CanBeCanceled
            ? cancellationCompletionSource.Task
            : NeverCompletingTask;

        var completedTask = await Task.WhenAny(task, timeoutTask, cancellationTask).ConfigureAwait(false);
        await timeoutCancellationTokenSource.CancelAsync().ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            return TaskWaitResult.CallerCanceled;
        }

        if (ReferenceEquals(completedTask, task))
        {
            return TaskWaitResult.Completed;
        }

        return ReferenceEquals(completedTask, cancellationTask)
            ? TaskWaitResult.CallerCanceled
            : TaskWaitResult.TimedOut;
    }

    private static async Task RequestCancellationAtDeadlineAsync (
        Task operationTask,
        ExecutionDeadline deadline,
        CancellationTokenSource cancellationTokenSource)
    {
        if (operationTask.IsCompleted)
        {
            return;
        }

        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            return;
        }

        using var deadlineCancellationTokenSource = new CancellationTokenSource();
        var deadlineTask = Task.Delay(
            remainingTimeout,
            deadline.Clock,
            deadlineCancellationTokenSource.Token);
        var completedTask = await Task.WhenAny(operationTask, deadlineTask).ConfigureAwait(false);
        if (ReferenceEquals(completedTask, operationTask) || operationTask.IsCompleted)
        {
            await deadlineCancellationTokenSource.CancelAsync().ConfigureAwait(false);
            return;
        }

        await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
    }

    private static OwnedOperationKey CreateOperationKey (
        ResolvedUnityProjectContext unityProject,
        DaemonOperationLane lane)
    {
        if (!Enum.IsDefined(lane))
        {
            throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unsupported daemon operation lane.");
        }

        return new OwnedOperationKey(
            Path.GetFullPath(unityProject.UnityProjectRoot),
            unityProject.ProjectFingerprint,
            lane);
    }

    private enum TaskWaitResult
    {
        Completed,
        TimedOut,
        CallerCanceled,
    }

    private readonly record struct OwnedOperationKey (
        string UnityProjectRoot,
        ProjectFingerprint ProjectFingerprint,
        DaemonOperationLane Lane);

    private sealed class OwnedCompensation : IDisposable
    {
        private readonly object sync = new();

        private readonly TaskCompletionSource quiescenceCompletionSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private IAsyncDisposable? lifecycleLease;

        private bool isReleased;

        public CancellationTokenSource CancellationTokenSource { get; } = new();

        public Task QuiescenceTask => quiescenceCompletionSource.Task;

        public Task? DeferredOwnershipTask { get; set; }

        public bool TryOwnLifecycleLease (IAsyncDisposable lease)
        {
            ArgumentNullException.ThrowIfNull(lease);

            lock (sync)
            {
                if (isReleased || lifecycleLease is not null)
                {
                    return false;
                }

                lifecycleLease = lease;
                return true;
            }
        }

        public IAsyncDisposable? MarkReleasedAndTakeLifecycleLease ()
        {
            lock (sync)
            {
                isReleased = true;
                var ownedLifecycleLease = lifecycleLease;
                lifecycleLease = null;
                return ownedLifecycleLease;
            }
        }

        public void RetainFailedLifecycleLease (IAsyncDisposable lease)
        {
            ArgumentNullException.ThrowIfNull(lease);

            lock (sync)
            {
                lifecycleLease = lease;
            }
        }

        public void SignalQuiescence ()
        {
            quiescenceCompletionSource.TrySetResult();
        }

        public void DisposeCancellationTokenSource ()
        {
            CancellationTokenSource.Dispose();
        }

        public void Dispose ()
        {
            DisposeCancellationTokenSource();
        }
    }
}
