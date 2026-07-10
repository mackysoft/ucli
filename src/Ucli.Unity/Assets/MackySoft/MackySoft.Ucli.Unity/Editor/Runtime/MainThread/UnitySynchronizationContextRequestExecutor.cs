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

        private readonly bool poisonOnActiveCancellation;

        private bool isProcessorActive;

        private bool isRunningInvocation;

        private IMainThreadInvocation? activeInvocation;

        private bool isDisposed;

        private string? poisonReason;

        private object? admissionSealToken;

        /// <summary> Initializes one main-thread executor with explicit ownership and capacity. </summary>
        /// <param name="mainThreadSynchronizationContext"> The captured Unity main-thread synchronization context. </param>
        /// <param name="mainThreadId"> The Unity main-thread identifier. </param>
        /// <param name="maxPendingInvocations"> The maximum number of invocations waiting behind a running invocation. </param>
        /// <param name="poisonOnActiveCancellation"> Whether a canceled non-terminal invocation poisons this lane. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="maxPendingInvocations" /> is not positive. </exception>
        internal UnitySynchronizationContextRequestExecutor (
            SynchronizationContext? mainThreadSynchronizationContext,
            int mainThreadId,
            int maxPendingInvocations,
            bool poisonOnActiveCancellation)
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
            this.poisonOnActiveCancellation = poisonOnActiveCancellation;

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
                    return poisonReason != null
                        || admissionSealToken != null
                        || isRunningInvocation
                        || pendingInvocations.Count > 0;
                }
            }
        }

        /// <inheritdoc />
        public bool IsPoisoned
        {
            get
            {
                lock (syncRoot)
                {
                    return poisonReason != null;
                }
            }
        }

        /// <inheritdoc />
        public void Poison (string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Mutation lane poison reason must not be empty.", nameof(reason));
            }

            List<IMainThreadInvocation>? failures = null;
            UnityMutationLanePoisonedException? exception = null;
            lock (syncRoot)
            {
                if (isDisposed || poisonReason != null)
                {
                    return;
                }

                poisonReason = reason;
                exception = new UnityMutationLanePoisonedException(reason);
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
        public bool TrySealAdmissionWhenIdle (out IDisposable admissionSeal)
        {
            lock (syncRoot)
            {
                RemoveCompletedPendingInvocations();
                if (isDisposed
                    || poisonReason != null
                    || admissionSealToken != null
                    || isRunningInvocation
                    || pendingInvocations.Count > 0)
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
        public void Dispose ()
        {
            EditorApplication.update -= ProcessQueueOnEditorUpdate;
            var exception = new ObjectDisposedException(nameof(UnitySynchronizationContextRequestExecutor));
            List<IMainThreadInvocation>? failures = null;
            lock (syncRoot)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
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

                if (poisonReason != null)
                {
                    return Task.FromException<T>(new UnityMutationLanePoisonedException(poisonReason));
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
            lock (syncRoot)
            {
                if (isDisposed)
                {
                    isProcessorActive = false;
                    return;
                }

                if (isRunningInvocation)
                {
                    return;
                }

                RemoveCompletedPendingInvocations();
                if (pendingInvocations.Count == 0)
                {
                    isProcessorActive = false;
                    return;
                }

                invocation = pendingInvocations.Dequeue();
                isRunningInvocation = true;
                activeInvocation = invocation;
            }

            _ = RunInvocationAsync(invocation);
        }

        /// <summary> Runs one queued invocation and releases execution gate after completion. </summary>
        /// <param name="invocation"> The queued invocation payload. </param>
        /// <returns> A task that completes after one invocation run finishes. </returns>
        private async Task RunInvocationAsync (IMainThreadInvocation invocation)
        {
            try
            {
                await invocation.RunAsync().ConfigureAwait(false);
            }
            finally
            {
                var shouldScheduleProcessor = false;
                lock (syncRoot)
                {
                    isRunningInvocation = false;
                    if (ReferenceEquals(activeInvocation, invocation))
                    {
                        activeInvocation = null;
                    }

                    if (pendingInvocations.Count == 0)
                    {
                        isProcessorActive = false;
                    }
                    else
                    {
                        shouldScheduleProcessor = true;
                    }
                }

                if (shouldScheduleProcessor)
                {
                    ScheduleProcessor();
                }
            }
        }

        private void OnInvocationCancellation (IMainThreadInvocation invocation)
        {
            var shouldAwaitQuiescence = false;
            lock (syncRoot)
            {
                shouldAwaitQuiescence = !isDisposed
                    && poisonReason == null
                    && ReferenceEquals(activeInvocation, invocation)
                    && invocation.HasNonTerminalWork;
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
        }

        private async Task CompleteActiveCancellationAfterQuiescenceGraceAsync (IMainThreadInvocation invocation)
        {
            var didQuiesce = await UnityMutationCancellationPolicy
                .WaitForQuiescenceAsync(invocation.TerminalCompletion)
                .ConfigureAwait(false);
            if (!didQuiesce)
            {
                if (poisonOnActiveCancellation)
                {
                    Poison(
                        "A canceled Unity mutation did not reach a terminal state and may still mutate Editor state.");
                }

                invocation.DetachNonTerminalWork();
            }

            invocation.TrySetCanceled();
        }

        /// <summary> Fails queued invocations when scheduling on synchronization context is unavailable. </summary>
        /// <param name="exception"> The scheduling exception propagated to queued invocations. </param>
        private void FailPendingInvocations (Exception exception)
        {
            List<IMainThreadInvocation>? failures = null;
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

        /// <summary> Represents one queued main-thread invocation. </summary>
        private interface IMainThreadInvocation
        {
            /// <summary> Gets whether user work has started and remains non-terminal. </summary>
            bool HasNonTerminalWork { get; }

            /// <summary> Gets the task that completes when user work can no longer mutate Unity state. </summary>
            Task TerminalCompletion { get; }

            /// <summary> Gets whether the outward completion was already published. </summary>
            bool IsOutwardCompleted { get; }

            /// <summary> Runs queued work item on the Unity main thread. </summary>
            Task RunAsync ();

            /// <summary> Completes the invocation as failed when main-thread scheduling cannot proceed. </summary>
            /// <param name="exception"> The scheduling failure. </param>
            void TrySetException (Exception exception);

            /// <summary> Completes the outward invocation as canceled after safety fencing. </summary>
            void TrySetCanceled ();

            /// <summary> Releases cancellation registration after a canceled queued item is removed. </summary>
            void DisposeCancellationRegistration ();

            /// <summary> Stops awaiting non-terminal work after mutation admission has been poisoned. </summary>
            void DetachNonTerminalWork ();
        }

        /// <summary> Represents one posted main-thread invocation with completion tracking. </summary>
        private sealed class MainThreadInvocation<T> : IMainThreadInvocation
        {
            private readonly Func<Task<T>> workItem;

            private readonly CancellationToken cancellationToken;

            private readonly Action<IMainThreadInvocation> onCancellation;

            private readonly CancellationTokenRegistration cancellationRegistration;

            private readonly TaskCompletionSource<bool> detachSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> terminalSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private Task<T>? activeWorkTask;

            private int workStarted;

            private int workTerminal;

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
            public bool HasNonTerminalWork
            {
                get
                {
                    if (Volatile.Read(ref workStarted) == 0)
                    {
                        return false;
                    }

                    var workTask = Volatile.Read(ref activeWorkTask);
                    return workTask == null
                        ? Volatile.Read(ref workTerminal) == 0
                        : !workTask.IsCompleted;
                }
            }

            /// <inheritdoc />
            public Task TerminalCompletion
            {
                get
                {
                    var workTask = Volatile.Read(ref activeWorkTask);
                    return workTask != null ? workTask : terminalSource.Task;
                }
            }

            /// <inheritdoc />
            public bool IsOutwardCompleted => CompletionSource.Task.IsCompleted;

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
                    Volatile.Write(ref workStarted, 1);
                    cancellationToken.ThrowIfCancellationRequested();
                    var workTask = workItem();
                    Volatile.Write(ref activeWorkTask, workTask);
                    TrackTerminalCompletion(workTask);
                    var completedTask = await Task.WhenAny(workTask, detachSource.Task).ConfigureAwait(false);
                    if (!ReferenceEquals(completedTask, workTask))
                    {
                        ObserveFault(workTask);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    var result = await workTask.ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    CompletionSource.TrySetResult(result);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    CompletionSource.TrySetCanceled();
                }
                catch (Exception exception)
                {
                    CompletionSource.TrySetException(exception);
                }
                finally
                {
                    Volatile.Write(ref workTerminal, 1);
                    if (Volatile.Read(ref activeWorkTask) == null)
                    {
                        terminalSource.TrySetResult(true);
                    }

                    cancellationRegistration.Dispose();
                }
            }

            private void TrackTerminalCompletion (Task workTask)
            {
                if (workTask == null)
                {
                    return;
                }

                _ = workTask.ContinueWith(
                    static (_, state) => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
                    terminalSource,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
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

            /// <inheritdoc />
            public void DetachNonTerminalWork ()
            {
                detachSource.TrySetResult(true);
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
    internal sealed class UnityMutationLanePoisonedException : UnityMutationLaneUnavailableException
    {
        public UnityMutationLanePoisonedException (string reason)
            : base($"Unity mutation safety is indeterminate. Restart the Unity Editor before sending another mutation. {reason}")
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
