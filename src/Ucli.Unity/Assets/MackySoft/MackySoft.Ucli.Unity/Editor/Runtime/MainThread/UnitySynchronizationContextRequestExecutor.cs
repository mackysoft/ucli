using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Executes asynchronous work items on Unity main-thread synchronization context. </summary>
    internal sealed class UnitySynchronizationContextRequestExecutor :
        IUnityMainThreadRequestExecutor,
        IUnityControlPlaneRequestExecutor,
        IUnityMutationRequestExecutionStartSource,
        IUnityMutationExecutionState,
        IUnityMutationLaneControl,
        IDisposable
    {
        internal const int DefaultMaxPendingInvocations = 64;

        private readonly object syncRoot = new object();

        private readonly Queue<IMainThreadInvocation> pendingInvocations = new Queue<IMainThreadInvocation>();

        private readonly SynchronizationContext? mainThreadSynchronizationContext;

        private readonly int mainThreadId;

        private readonly int maxPendingInvocations;

        private readonly HashSet<Task> retirementDependencies = new HashSet<Task>();

        private bool isProcessorActive;

        private bool isRunningInvocation;

        private IMainThreadInvocation? activeInvocation;

        private IMainThreadInvocation? preparedInvocation;

        private bool isDisposed;

        private string? quarantineReason;

        private object? admissionSealToken;

        private TaskCompletionSource<bool>? retirementCompletionSource;

        /// <inheritdoc />
        public event Func<CancellationToken, Task>? RequestExecutionStarting;

        /// <summary> Initializes one main-thread executor with explicit ownership and capacity. </summary>
        /// <param name="mainThreadSynchronizationContext"> The captured Unity main-thread synchronization context. </param>
        /// <param name="mainThreadId"> The Unity main-thread identifier. </param>
        /// <param name="maxPendingInvocations"> The maximum number of invocations waiting behind a running invocation. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="maxPendingInvocations" /> is not positive. </exception>
        internal UnitySynchronizationContextRequestExecutor (
            SynchronizationContext? mainThreadSynchronizationContext,
            int mainThreadId,
            int maxPendingInvocations)
        {
            if (maxPendingInvocations <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPendingInvocations),
                    maxPendingInvocations,
                    "Maximum pending invocation count must be greater than zero.");
            }

            this.mainThreadSynchronizationContext = mainThreadSynchronizationContext;
            this.mainThreadId = mainThreadId;
            this.maxPendingInvocations = maxPendingInvocations;

            // NOTE:
            // Unity can replace or temporarily stop servicing the captured SynchronizationContext while the
            // editor finishes GUI startup or recovers from script reload. Keep the update-loop pump attached as
            // the authoritative main-thread drain, and use SynchronizationContext.Post only as an eager wake-up.
            EditorApplication.update += ProcessQueueOnEditorUpdate;
        }

        /// <inheritdoc />
        public bool IsBusy
        {
            get
            {
                lock (syncRoot)
                {
                    RemoveCompletedPendingInvocations();
                    return quarantineReason != null
                        || admissionSealToken != null
                        || isRunningInvocation
                        || pendingInvocations.Count > 0;
                }
            }
        }

        /// <inheritdoc />
        public bool HasUnfinishedWork
        {
            get
            {
                lock (syncRoot)
                {
                    RemoveCompletedPendingInvocations();
                    RemoveCompletedRetirementDependencies();
                    return HasUnfinishedWorkLocked();
                }
            }
        }

        /// <inheritdoc />
        public bool IsQuarantined
        {
            get
            {
                lock (syncRoot)
                {
                    return quarantineReason != null;
                }
            }
        }

        /// <inheritdoc />
        public IUnityMutationActivity BeginMutation ()
        {
            lock (syncRoot)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(UnitySynchronizationContextRequestExecutor));
                }

                if (activeInvocation == null)
                {
                    throw new InvalidOperationException(
                        "A Unity mutation can start only from the request currently executing on this lane.");
                }

                return activeInvocation.BeginMutation();
            }
        }

        /// <inheritdoc />
        public void Quarantine (string reason, Task mutationCompletion)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Mutation lane quarantine reason must not be empty.", nameof(reason));
            }

            if (mutationCompletion == null)
            {
                throw new ArgumentNullException(nameof(mutationCompletion));
            }

            List<IMainThreadInvocation>? failures = null;
            UnityMutationLaneQuarantinedException? exception = null;
            var trackDependency = false;
            TaskCompletionSource<bool>? retirementSource;
            lock (syncRoot)
            {
                if (isDisposed)
                {
                    return;
                }

                if (quarantineReason == null)
                {
                    if (mutationCompletion.Status == TaskStatus.RanToCompletion)
                    {
                        return;
                    }

                    if (activeInvocation == null || !activeInvocation.HasActiveMutation)
                    {
                        throw new InvalidOperationException(
                            "The current request cannot quarantine the lane because it has no unfinished started Unity mutation.");
                    }

                    quarantineReason = reason;
                }

                exception = new UnityMutationLaneQuarantinedException(quarantineReason);
                if (!mutationCompletion.IsCompleted && retirementDependencies.Add(mutationCompletion))
                {
                    EnsureRetirementCompletionSourceLocked();
                    trackDependency = true;
                }

                if (pendingInvocations.Count > 0)
                {
                    failures = new List<IMainThreadInvocation>(pendingInvocations.Count);
                    while (pendingInvocations.Count > 0)
                    {
                        failures.Add(pendingInvocations.Dequeue());
                    }
                }

                if (!isRunningInvocation)
                {
                    isProcessorActive = false;
                }

                retirementSource = TakeCompletedRetirementSourceLocked();
            }

            retirementSource?.TrySetResult(true);

            if (trackDependency)
            {
                _ = mutationCompletion.ContinueWith(
                    static (_, state) =>
                    {
                        var dependencyState = (RetirementDependencyState)state!;
                        dependencyState.Owner.OnRetirementDependencyCompleted(dependencyState.Dependency);
                    },
                    new RetirementDependencyState(this, mutationCompletion),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            if (failures == null)
            {
                return;
            }

            foreach (var failure in failures)
            {
                failure.TrySetException(exception);
            }
        }

        /// <inheritdoc />
        public bool TrySealAdmissionForRetirement (out IDisposable admissionSeal)
        {
            lock (syncRoot)
            {
                RemoveCompletedPendingInvocations();
                if (isDisposed
                    || admissionSealToken != null
                    || (quarantineReason == null
                        && (isRunningInvocation || pendingInvocations.Count > 0)))
                {
                    admissionSeal = null;
                    return false;
                }

                var sealToken = new object();
                admissionSealToken = sealToken;
                admissionSeal = new MutationAdmissionSeal(this, sealToken);
                return true;
            }
        }

        /// <inheritdoc />
        public Task WaitForRetirementAsync ()
        {
            lock (syncRoot)
            {
                RemoveCompletedPendingInvocations();
                RemoveCompletedRetirementDependencies();
                if (!HasUnfinishedWorkLocked())
                {
                    return Task.CompletedTask;
                }

                return EnsureRetirementCompletionSourceLocked().Task;
            }
        }

        /// <inheritdoc />
        public void Dispose ()
        {
            EditorApplication.update -= ProcessQueueOnEditorUpdate;
            var exception = new ObjectDisposedException(nameof(UnitySynchronizationContextRequestExecutor));
            List<IMainThreadInvocation>? failures = null;
            IMainThreadInvocation? preparedFailure = null;
            TaskCompletionSource<bool>? retirementSource;
            lock (syncRoot)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                preparedFailure = preparedInvocation;
                preparedInvocation = null;
                if (pendingInvocations.Count > 0)
                {
                    failures = new List<IMainThreadInvocation>(pendingInvocations.Count);
                    while (pendingInvocations.Count > 0)
                    {
                        failures.Add(pendingInvocations.Dequeue());
                    }
                }

                if (!isRunningInvocation)
                {
                    isProcessorActive = false;
                }

                retirementSource = TakeCompletedRetirementSourceLocked();
            }

            retirementSource?.TrySetResult(true);

            if (preparedFailure != null)
            {
                preparedFailure.TrySetException(exception);
                CompleteInvocation(preparedFailure);
            }

            if (failures == null)
            {
                return;
            }

            foreach (var failure in failures)
            {
                failure.TrySetException(exception);
            }
        }

        /// <summary> Executes one asynchronous work item on Unity main thread. </summary>
        /// <typeparam name="T"> The work-item result type. </typeparam>
        /// <param name="workItem"> The asynchronous work item to execute. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by connection handling. </param>
        /// <returns> The work-item result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="workItem" /> is <see langword="null" />. </exception>
        /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled. </exception>
        public Task<T> ExecuteAsync<T> (
            Func<Task<T>> workItem,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            MainThreadInvocation<T> invocation;
            var shouldScheduleProcessor = false;
            lock (syncRoot)
            {
                if (isDisposed)
                {
                    return Task.FromException<T>(new ObjectDisposedException(nameof(UnitySynchronizationContextRequestExecutor)));
                }

                if (quarantineReason != null)
                {
                    return Task.FromException<T>(new UnityMutationLaneQuarantinedException(quarantineReason));
                }

                if (admissionSealToken != null)
                {
                    return Task.FromException<T>(new UnityMutationLaneAdmissionSealedException());
                }

                RemoveCompletedPendingInvocations();
                if (pendingInvocations.Count >= maxPendingInvocations)
                {
                    return Task.FromException<T>(new UnityMainThreadRequestQueueFullException(maxPendingInvocations));
                }

                invocation = new MainThreadInvocation<T>(
                    workItem,
                    cancellationToken,
                    OnInvocationCancellation);
                pendingInvocations.Enqueue(invocation);
                EnsureRetirementCompletionSourceLocked();
                if (!isProcessorActive)
                {
                    isProcessorActive = true;
                    shouldScheduleProcessor = true;
                }
            }

            if (shouldScheduleProcessor)
            {
                ScheduleProcessor();
            }

            return invocation.Completion;
        }

        private void ReleaseAdmissionSeal (object sealToken)
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(admissionSealToken, sealToken))
                {
                    admissionSealToken = null;
                }
            }
        }

        private TaskCompletionSource<bool> EnsureRetirementCompletionSourceLocked ()
        {
            if (retirementCompletionSource == null || retirementCompletionSource.Task.IsCompleted)
            {
                retirementCompletionSource = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            return retirementCompletionSource;
        }

        private bool HasUnfinishedWorkLocked ()
        {
            return isRunningInvocation
                || pendingInvocations.Count > 0
                || retirementDependencies.Count > 0;
        }

        private void RemoveCompletedRetirementDependencies ()
        {
            retirementDependencies.RemoveWhere(static dependency => dependency.IsCompleted);
        }

        private TaskCompletionSource<bool>? TakeCompletedRetirementSourceLocked ()
        {
            RemoveCompletedRetirementDependencies();
            if (HasUnfinishedWorkLocked())
            {
                return null;
            }

            var completionSource = retirementCompletionSource;
            retirementCompletionSource = null;
            return completionSource;
        }

        private void OnRetirementDependencyCompleted (Task dependency)
        {
            TaskCompletionSource<bool>? completionSource;
            lock (syncRoot)
            {
                retirementDependencies.Remove(dependency);
                completionSource = TakeCompletedRetirementSourceLocked();
            }

            completionSource?.TrySetResult(true);
        }

        /// <summary> Schedules queue processing on Unity main thread synchronization context. </summary>
        private void ScheduleProcessor ()
        {
            try
            {
                if (Thread.CurrentThread.ManagedThreadId == mainThreadId)
                {
                    ProcessQueueOnMainThread();
                    return;
                }

                if (mainThreadSynchronizationContext == null)
                {
                    return;
                }

                mainThreadSynchronizationContext.Post(static state =>
                {
                    var executor = (UnitySynchronizationContextRequestExecutor)state!;
                    executor.ProcessQueueOnMainThread();
                }, this);
            }
            catch (Exception exception)
            {
                FailPendingInvocations(exception);
            }
        }

        /// <summary> Processes queued invocations during the Unity editor update loop. </summary>
        private void ProcessQueueOnEditorUpdate ()
        {
            ProcessQueueOnMainThread();
        }

        /// <summary> Processes queued main-thread invocations via Unity synchronization context. </summary>
        private void ProcessQueueOnMainThread ()
        {
            IMainThreadInvocation? invocation = null;
            var prepareExecutionStart = false;
            lock (syncRoot)
            {
                if (isDisposed)
                {
                    isProcessorActive = false;
                    return;
                }

                if (isRunningInvocation)
                {
                    if (preparedInvocation == null)
                    {
                        return;
                    }

                    invocation = preparedInvocation;
                    preparedInvocation = null;
                }
                else
                {
                    RemoveCompletedPendingInvocations();
                    if (pendingInvocations.Count == 0)
                    {
                        isProcessorActive = false;
                        return;
                    }

                    invocation = pendingInvocations.Dequeue();
                    isRunningInvocation = true;
                    activeInvocation = invocation;
                    prepareExecutionStart = true;
                }
            }

            if (prepareExecutionStart)
            {
                _ = PrepareInvocationAsync(invocation);
                return;
            }

            _ = RunInvocationAsync(invocation);
        }

        private async Task NotifyRequestExecutionStartingAsync (CancellationToken cancellationToken)
        {
            var handlers = RequestExecutionStarting;
            if (handlers == null)
            {
                return;
            }

            foreach (Func<CancellationToken, Task> handler in handlers.GetInvocationList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var barrierTask = handler(cancellationToken);
                if (barrierTask == null)
                {
                    throw new InvalidOperationException(
                        "A Unity mutation execution-start barrier returned a null task.");
                }

                await barrierTask.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private async Task PrepareInvocationAsync (IMainThreadInvocation invocation)
        {
            try
            {
                await NotifyRequestExecutionStartingAsync(invocation.CancellationToken).ConfigureAwait(false);
                lock (syncRoot)
                {
                    if (isDisposed)
                    {
                        throw new ObjectDisposedException(nameof(UnitySynchronizationContextRequestExecutor));
                    }

                    if (!isRunningInvocation || !ReferenceEquals(activeInvocation, invocation))
                    {
                        throw new InvalidOperationException(
                            "The Unity mutation request lost execution ownership before its start barrier completed.");
                    }

                    if (preparedInvocation != null)
                    {
                        throw new InvalidOperationException(
                            "Another Unity mutation request is already prepared for main-thread execution.");
                    }

                    preparedInvocation = invocation;
                }

                // The barrier can complete on a worker thread or a stale synchronization context. The editor
                // update pump remains authoritative for invoking the request delegate on Unity's main thread.
                ScheduleProcessor();
                return;
            }
            catch (OperationCanceledException) when (invocation.CancellationToken.IsCancellationRequested)
            {
                invocation.TrySetCanceled();
            }
            catch (Exception exception)
            {
                invocation.TrySetException(exception);
            }

            CompleteInvocation(invocation);
        }

        /// <summary> Runs one queued invocation and releases execution gate after completion. </summary>
        /// <param name="invocation"> The queued invocation payload. </param>
        /// <returns> A task that completes after one invocation run finishes. </returns>
        private async Task RunInvocationAsync (IMainThreadInvocation invocation)
        {
            try
            {
                await invocation.RunAsync().ConfigureAwait(false);
                if (invocation.HasActiveMutation)
                {
                    Quarantine(
                        "A Unity mutation request terminated before its mutation reached a safe state.",
                        invocation.MutationCompletion);
                    await invocation.MutationCompletion.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (invocation.CancellationToken.IsCancellationRequested)
            {
                invocation.TrySetCanceled();
            }
            catch (Exception exception)
            {
                invocation.TrySetException(exception);
            }
            finally
            {
                CompleteInvocation(invocation);
            }
        }

        private void CompleteInvocation (IMainThreadInvocation invocation)
        {
            var shouldScheduleProcessor = false;
            TaskCompletionSource<bool>? retirementSource;
            lock (syncRoot)
            {
                if (ReferenceEquals(preparedInvocation, invocation))
                {
                    preparedInvocation = null;
                }

                isRunningInvocation = false;
                if (ReferenceEquals(activeInvocation, invocation))
                {
                    activeInvocation = null;
                }

                if (quarantineReason != null || pendingInvocations.Count == 0)
                {
                    isProcessorActive = false;
                }
                else
                {
                    shouldScheduleProcessor = true;
                }

                retirementSource = TakeCompletedRetirementSourceLocked();
            }

            retirementSource?.TrySetResult(true);

            if (shouldScheduleProcessor)
            {
                ScheduleProcessor();
            }
        }

        private void OnInvocationCancellation (IMainThreadInvocation invocation)
        {
            var shouldAwaitQuiescence = false;
            lock (syncRoot)
            {
                shouldAwaitQuiescence = !isDisposed
                    && quarantineReason == null
                    && ReferenceEquals(activeInvocation, invocation)
                    && invocation.HasActiveMutation;
            }

            if (!shouldAwaitQuiescence)
            {
                invocation.TrySetCanceled();
                return;
            }

            _ = CompleteActiveCancellationAfterQuiescenceGraceAsync(invocation);
        }

        private void RemoveCompletedPendingInvocations ()
        {
            var pendingCount = pendingInvocations.Count;
            for (var index = 0; index < pendingCount; index++)
            {
                var invocation = pendingInvocations.Dequeue();
                if (invocation.IsOutwardCompleted)
                {
                    invocation.DisposeCancellationRegistration();
                    continue;
                }

                pendingInvocations.Enqueue(invocation);
            }

            if (!HasUnfinishedWorkLocked())
            {
                var completionSource = retirementCompletionSource;
                retirementCompletionSource = null;
                completionSource?.TrySetResult(true);
            }
        }

        private async Task CompleteActiveCancellationAfterQuiescenceGraceAsync (IMainThreadInvocation invocation)
        {
            var didQuiesce = await UnityMutationCancellationPolicy
                .WaitForQuiescenceAsync(invocation.MutationCompletion)
                .ConfigureAwait(false);
            if (!didQuiesce)
            {
                Quarantine(
                    "A canceled Unity mutation did not reach a safe state within the cancellation grace period.",
                    invocation.MutationCompletion);
            }

            invocation.TrySetCanceled();
        }

        /// <summary> Fails queued invocations when scheduling on synchronization context is unavailable. </summary>
        /// <param name="exception"> The scheduling exception propagated to queued invocations. </param>
        private void FailPendingInvocations (Exception exception)
        {
            List<IMainThreadInvocation>? failures = null;
            TaskCompletionSource<bool>? retirementSource;
            lock (syncRoot)
            {
                if (pendingInvocations.Count > 0)
                {
                    failures = new List<IMainThreadInvocation>(pendingInvocations.Count);
                    while (pendingInvocations.Count > 0)
                    {
                        failures.Add(pendingInvocations.Dequeue());
                    }
                }

                if (!isRunningInvocation)
                {
                    isProcessorActive = false;
                }

                retirementSource = TakeCompletedRetirementSourceLocked();
            }

            retirementSource?.TrySetResult(true);

            if (failures == null)
            {
                return;
            }

            foreach (var failure in failures)
            {
                failure.TrySetException(exception);
            }
        }

        /// <summary> Represents one queued main-thread invocation. </summary>
        private interface IMainThreadInvocation
        {
            /// <summary> Gets the request cancellation token. </summary>
            CancellationToken CancellationToken { get; }

            /// <summary> Gets whether a Unity mutation started and has not yet reached its safe state. </summary>
            bool HasActiveMutation { get; }

            /// <summary> Gets the task that completes when the explicit Unity mutation reaches its safe state. </summary>
            Task MutationCompletion { get; }

            /// <summary> Gets whether the outward completion was already published. </summary>
            bool IsOutwardCompleted { get; }

            /// <summary> Runs queued work item on the Unity main thread. </summary>
            Task RunAsync ();

            /// <summary> Starts the request's explicit Unity-mutation activity. </summary>
            IUnityMutationActivity BeginMutation ();

            /// <summary> Completes the invocation as failed when main-thread scheduling cannot proceed. </summary>
            /// <param name="exception"> The scheduling failure. </param>
            void TrySetException (Exception exception);

            /// <summary> Completes the outward invocation as canceled after safety fencing. </summary>
            void TrySetCanceled ();

            /// <summary> Releases cancellation registration after a canceled queued item is removed. </summary>
            void DisposeCancellationRegistration ();

        }

        /// <summary> Represents one posted main-thread invocation with completion tracking. </summary>
        private sealed class MainThreadInvocation<T> : IMainThreadInvocation
        {
            private readonly Func<Task<T>> workItem;

            private readonly CancellationToken cancellationToken;

            private readonly Action<IMainThreadInvocation> onCancellation;

            private readonly CancellationTokenRegistration cancellationRegistration;

            private readonly object mutationSyncRoot = new object();

            private MutationActivity? mutationActivity;

            /// <summary> Initializes a new instance of the <see cref="MainThreadInvocation" /> class. </summary>
            /// <param name="workItem"> The work item delegate. </param>
            /// <param name="cancellationToken"> The cancellation token for invocation. </param>
            public MainThreadInvocation (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken,
                Action<IMainThreadInvocation> onCancellation)
            {
                this.workItem = workItem;
                this.cancellationToken = cancellationToken;
                this.onCancellation = onCancellation ?? throw new ArgumentNullException(nameof(onCancellation));
                CompletionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(static state =>
                    {
                        var invocation = (MainThreadInvocation<T>)state!;
                        invocation.onCancellation(invocation);
                    }, this);
                }
            }

            /// <inheritdoc />
            public bool HasActiveMutation
            {
                get
                {
                    lock (mutationSyncRoot)
                    {
                        return mutationActivity != null && !mutationActivity.Completion.IsCompleted;
                    }
                }
            }

            /// <inheritdoc />
            public Task MutationCompletion
            {
                get
                {
                    lock (mutationSyncRoot)
                    {
                        return mutationActivity?.Completion ?? Task.CompletedTask;
                    }
                }
            }

            /// <inheritdoc />
            public bool IsOutwardCompleted => CompletionSource.Task.IsCompleted;

            /// <inheritdoc />
            public CancellationToken CancellationToken => cancellationToken;

            /// <summary> Gets the completion task for this invocation. </summary>
            public Task<T> Completion => CompletionSource.Task;

            /// <summary> Gets the completion source used by this invocation. </summary>
            private TaskCompletionSource<T> CompletionSource { get; }

            /// <summary> Runs posted request delegate and completes invocation result. </summary>
            /// <returns> A task that completes after invocation result has been published. </returns>
            public async Task RunAsync ()
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var workTask = workItem();
                    if (workTask == null)
                    {
                        throw new InvalidOperationException("Unity main-thread work returned a null task.");
                    }

                    var result = await workTask.ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    CompletionSource.TrySetResult(result);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // OnInvocationCancellation owns outward cancellation so a started mutation cannot
                    // publish cancellation before it either reaches a safe state or is quarantined.
                }
                catch (Exception exception)
                {
                    CompletionSource.TrySetException(exception);
                }
                finally
                {
                    cancellationRegistration.Dispose();
                }
            }

            /// <inheritdoc />
            public IUnityMutationActivity BeginMutation ()
            {
                lock (mutationSyncRoot)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (mutationActivity != null)
                    {
                        throw new InvalidOperationException(
                            "One main-thread request cannot start more than one Unity mutation activity.");
                    }

                    mutationActivity = new MutationActivity();
                    return mutationActivity;
                }
            }

            /// <summary> Completes invocation as failed when scheduling on main thread cannot proceed. </summary>
            /// <param name="exception"> The scheduling failure. </param>
            public void TrySetException (Exception exception)
            {
                CompletionSource.TrySetException(exception);
                cancellationRegistration.Dispose();
            }

            /// <inheritdoc />
            public void TrySetCanceled ()
            {
                CompletionSource.TrySetCanceled();
            }

            /// <inheritdoc />
            public void DisposeCancellationRegistration ()
            {
                cancellationRegistration.Dispose();
            }

            private sealed class MutationActivity : IUnityMutationActivity
            {
                private readonly TaskCompletionSource<bool> completionSource =
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                public Task Completion => completionSource.Task;

                public void Complete ()
                {
                    completionSource.TrySetResult(true);
                }
            }
        }

        private sealed record RetirementDependencyState (
            UnitySynchronizationContextRequestExecutor Owner,
            Task Dependency);

        private sealed class MutationAdmissionSeal : IDisposable
        {
            private readonly UnitySynchronizationContextRequestExecutor owner;

            private readonly object sealToken;

            private int disposed;

            public MutationAdmissionSeal (
                UnitySynchronizationContextRequestExecutor owner,
                object sealToken)
            {
                this.owner = owner;
                this.sealToken = sealToken;
            }

            public void Dispose ()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                {
                    owner.ReleaseAdmissionSeal(sealToken);
                }
            }
        }
    }

    /// <summary> Indicates that the mutation lane cannot safely accept additional work. </summary>
    internal abstract class UnityMutationLaneUnavailableException : InvalidOperationException
    {
        protected UnityMutationLaneUnavailableException (string message)
            : base(message)
        {
        }
    }

    /// <summary> Indicates that the bounded Unity main-thread request queue cannot accept another invocation. </summary>
    internal sealed class UnityMainThreadRequestQueueFullException : UnityMutationLaneUnavailableException
    {
        /// <summary> Initializes a new instance of the <see cref="UnityMainThreadRequestQueueFullException" /> class. </summary>
        /// <param name="maxPendingInvocations"> The configured maximum number of pending invocations. </param>
        public UnityMainThreadRequestQueueFullException (int maxPendingInvocations)
            : base($"Unity main-thread request queue is full. maxPendingInvocations={maxPendingInvocations}.")
        {
        }
    }

    /// <summary> Indicates that an unfinished mutation made the current host generation unsafe to reuse. </summary>
    internal sealed class UnityMutationLaneQuarantinedException : UnityMutationLaneUnavailableException
    {
        public UnityMutationLaneQuarantinedException (string reason)
            : base($"The current Unity mutation host generation is quarantined and cannot accept additional work. {reason}")
        {
        }
    }

    /// <summary> Indicates that mutation admission was sealed while the current daemon generation is replaced. </summary>
    internal sealed class UnityMutationLaneAdmissionSealedException : UnityMutationLaneUnavailableException
    {
        public UnityMutationLaneAdmissionSealedException ()
            : base("Unity mutation admission is sealed while the GUI daemon session is being replaced.")
        {
        }
    }
}
