using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary>
    /// Starts control-plane requests on the Unity main thread without serializing their asynchronous wait lifetime.
    /// </summary>
    internal sealed class UnityControlPlaneRequestExecutor :
        IUnityControlPlaneRequestExecutor,
        IUnityControlPlaneRequestLifetime,
        IDisposable
    {
        internal const int DefaultMaxConcurrentInvocations = 64;

        private readonly object syncRoot = new object();

        private readonly Queue<IControlPlaneInvocation> pendingInvocations = new Queue<IControlPlaneInvocation>();

        private readonly HashSet<IControlPlaneInvocation> activeInvocations = new HashSet<IControlPlaneInvocation>();

        private readonly SynchronizationContext? mainThreadSynchronizationContext;

        private readonly int mainThreadId;

        private readonly int maxConcurrentInvocations;

        private TaskCompletionSource<bool> retirementCompletionSource = CreateCompletedRetirementSource();

        private bool isWakeUpScheduled;

        private bool isDisposed;

        /// <inheritdoc />
        public bool HasUnfinishedWork
        {
            get
            {
                lock (syncRoot)
                {
                    RemoveCanceledPendingInvocations();
                    return pendingInvocations.Count > 0 || activeInvocations.Count > 0;
                }
            }
        }

        /// <summary> Initializes a bounded control-plane executor bound to the Unity main thread. </summary>
        /// <param name="mainThreadSynchronizationContext"> The captured Unity main-thread synchronization context. </param>
        /// <param name="mainThreadId"> The Unity main-thread identifier. </param>
        /// <param name="maxConcurrentInvocations"> The maximum number of admitted pending and active requests. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="maxConcurrentInvocations" /> is not positive. </exception>
        internal UnityControlPlaneRequestExecutor (
            SynchronizationContext? mainThreadSynchronizationContext,
            int mainThreadId,
            int maxConcurrentInvocations)
        {
            if (maxConcurrentInvocations <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentInvocations),
                    maxConcurrentInvocations,
                    "Maximum concurrent invocation count must be greater than zero.");
            }

            this.mainThreadSynchronizationContext = mainThreadSynchronizationContext;
            this.mainThreadId = mainThreadId;
            this.maxConcurrentInvocations = maxConcurrentInvocations;
            EditorApplication.update += ProcessPendingOnEditorUpdate;
        }

        /// <inheritdoc />
        public Task<T> ExecuteAsync<T> (
            Func<Task<T>> workItem,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            ControlPlaneInvocation<T> invocation;
            var shouldScheduleWakeUp = false;
            lock (syncRoot)
            {
                if (isDisposed)
                {
                    return Task.FromException<T>(new ObjectDisposedException(nameof(UnityControlPlaneRequestExecutor)));
                }

                RemoveCanceledPendingInvocations();
                if (pendingInvocations.Count + activeInvocations.Count >= maxConcurrentInvocations)
                {
                    return Task.FromException<T>(
                        new UnityControlPlaneCapacityExceededException(maxConcurrentInvocations));
                }

                if (pendingInvocations.Count == 0 && activeInvocations.Count == 0)
                {
                    retirementCompletionSource = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }

                invocation = new ControlPlaneInvocation<T>(
                    workItem,
                    cancellationToken,
                    OnInvocationTerminated);
                pendingInvocations.Enqueue(invocation);
                if (!isWakeUpScheduled)
                {
                    isWakeUpScheduled = true;
                    shouldScheduleWakeUp = true;
                }
            }

            if (Thread.CurrentThread.ManagedThreadId == mainThreadId)
            {
                ProcessPendingOnMainThread();
            }
            else if (shouldScheduleWakeUp)
            {
                PostWakeUp();
            }

            return invocation.Completion;
        }

        /// <inheritdoc />
        public Task WaitForRetirementAsync ()
        {
            lock (syncRoot)
            {
                RemoveCanceledPendingInvocations();
                return retirementCompletionSource.Task;
            }
        }

        /// <inheritdoc />
        public void Dispose ()
        {
            List<IControlPlaneInvocation> invocations;
            lock (syncRoot)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                isWakeUpScheduled = false;
                invocations = new List<IControlPlaneInvocation>(
                    pendingInvocations.Count + activeInvocations.Count);
                while (pendingInvocations.Count > 0)
                {
                    invocations.Add(pendingInvocations.Dequeue());
                }

                invocations.AddRange(activeInvocations);
                CompleteRetirementIfIdle();
            }

            EditorApplication.update -= ProcessPendingOnEditorUpdate;
            var exception = new ObjectDisposedException(nameof(UnityControlPlaneRequestExecutor));
            for (var index = 0; index < invocations.Count; index++)
            {
                invocations[index].TrySetException(exception);
            }
        }

        private void PostWakeUp ()
        {
            if (mainThreadSynchronizationContext == null)
            {
                return;
            }

            try
            {
                mainThreadSynchronizationContext.Post(static state =>
                {
                    var executor = (UnityControlPlaneRequestExecutor)state!;
                    executor.ProcessPendingOnMainThread();
                }, this);
            }
            catch (Exception exception)
            {
                FailPendingInvocations(exception);
            }
        }

        private void ProcessPendingOnEditorUpdate ()
        {
            ProcessPendingOnMainThread();
        }

        private void ProcessPendingOnMainThread ()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
            {
                lock (syncRoot)
                {
                    isWakeUpScheduled = false;
                }

                return;
            }

            while (true)
            {
                IControlPlaneInvocation? invocation;
                lock (syncRoot)
                {
                    if (isDisposed)
                    {
                        isWakeUpScheduled = false;
                        return;
                    }

                    RemoveCanceledPendingInvocations();
                    if (pendingInvocations.Count == 0)
                    {
                        isWakeUpScheduled = false;
                        return;
                    }

                    invocation = pendingInvocations.Dequeue();
                    activeInvocations.Add(invocation);
                }

                invocation.Start();
            }
        }

        private void OnInvocationTerminated (IControlPlaneInvocation invocation)
        {
            lock (syncRoot)
            {
                activeInvocations.Remove(invocation);
                CompleteRetirementIfIdle();
            }
        }

        private void RemoveCanceledPendingInvocations ()
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

            CompleteRetirementIfIdle();
        }

        private void CompleteRetirementIfIdle ()
        {
            if (pendingInvocations.Count == 0 && activeInvocations.Count == 0)
            {
                retirementCompletionSource.TrySetResult(true);
            }
        }

        private void FailPendingInvocations (Exception exception)
        {
            List<IControlPlaneInvocation>? failures = null;
            lock (syncRoot)
            {
                isWakeUpScheduled = false;
                if (pendingInvocations.Count > 0)
                {
                    failures = new List<IControlPlaneInvocation>(pendingInvocations.Count);
                    while (pendingInvocations.Count > 0)
                    {
                        failures.Add(pendingInvocations.Dequeue());
                    }

                    CompleteRetirementIfIdle();
                }
            }

            if (failures == null)
            {
                return;
            }

            for (var index = 0; index < failures.Count; index++)
            {
                failures[index].TrySetException(exception);
            }
        }

        private interface IControlPlaneInvocation
        {
            bool IsOutwardCompleted { get; }

            void Start ();

            void TrySetException (Exception exception);

            void DisposeCancellationRegistration ();
        }

        private static TaskCompletionSource<bool> CreateCompletedRetirementSource ()
        {
            var completionSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            completionSource.SetResult(true);
            return completionSource;
        }

        private sealed class ControlPlaneInvocation<T> : IControlPlaneInvocation
        {
            private readonly Func<Task<T>> workItem;

            private readonly CancellationToken cancellationToken;

            private readonly Action<IControlPlaneInvocation> onTerminated;

            private readonly CancellationTokenRegistration cancellationRegistration;

            private readonly TaskCompletionSource<T> completionSource =
                new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            public ControlPlaneInvocation (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken,
                Action<IControlPlaneInvocation> onTerminated)
            {
                this.workItem = workItem;
                this.cancellationToken = cancellationToken;
                this.onTerminated = onTerminated;
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(static state =>
                    {
                        var invocation = (ControlPlaneInvocation<T>)state!;
                        invocation.completionSource.TrySetCanceled();
                    }, this);
                }
            }

            public bool IsOutwardCompleted => completionSource.Task.IsCompleted;

            public Task<T> Completion => completionSource.Task;

            public void Start ()
            {
                if (completionSource.Task.IsCompleted)
                {
                    cancellationRegistration.Dispose();
                    onTerminated(this);
                    return;
                }

                Task<T> workTask;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    workTask = workItem()
                        ?? throw new InvalidOperationException("Unity control-plane work returned a null task.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    completionSource.TrySetCanceled();
                    cancellationRegistration.Dispose();
                    onTerminated(this);
                    return;
                }
                catch (Exception exception)
                {
                    completionSource.TrySetException(exception);
                    cancellationRegistration.Dispose();
                    onTerminated(this);
                    return;
                }

                _ = CompleteFromWorkAsync(workTask);
            }

            public void TrySetException (Exception exception)
            {
                completionSource.TrySetException(exception);
                cancellationRegistration.Dispose();
            }

            public void DisposeCancellationRegistration ()
            {
                cancellationRegistration.Dispose();
            }

            private async Task CompleteFromWorkAsync (Task<T> workTask)
            {
                try
                {
                    var result = await workTask.ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    completionSource.TrySetResult(result);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    completionSource.TrySetCanceled();
                }
                catch (Exception exception)
                {
                    completionSource.TrySetException(exception);
                }
                finally
                {
                    cancellationRegistration.Dispose();
                    onTerminated(this);
                }
            }
        }
    }

    /// <summary> Indicates that the bounded control-plane executor cannot admit another request. </summary>
    internal sealed class UnityControlPlaneCapacityExceededException : InvalidOperationException
    {
        public UnityControlPlaneCapacityExceededException (int maxConcurrentInvocations)
            : base($"Unity control-plane request capacity is exhausted. maxConcurrentInvocations={maxConcurrentInvocations}.")
        {
        }
    }
}
