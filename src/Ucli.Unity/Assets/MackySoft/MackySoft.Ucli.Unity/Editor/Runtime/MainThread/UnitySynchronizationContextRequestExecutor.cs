using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Executes asynchronous work items on Unity main-thread synchronization context. </summary>
    internal sealed class UnitySynchronizationContextRequestExecutor : IUnityMainThreadRequestExecutor
    {
        private readonly object syncRoot = new object();

        private readonly Queue<IMainThreadInvocation> pendingInvocations = new Queue<IMainThreadInvocation>();

        private readonly SynchronizationContext? mainThreadSynchronizationContext;

        private readonly int mainThreadId;

        private bool isProcessorActive;

        private bool isRunningInvocation;

        /// <summary> Initializes a new instance of the <see cref="UnitySynchronizationContextRequestExecutor" /> class. </summary>
        public UnitySynchronizationContextRequestExecutor ()
            : this(SynchronizationContext.Current, Thread.CurrentThread.ManagedThreadId)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UnitySynchronizationContextRequestExecutor" /> class for tests. </summary>
        /// <param name="mainThreadSynchronizationContext"> The captured Unity main-thread synchronization context. </param>
        /// <param name="mainThreadId"> The Unity main-thread identifier. </param>
        internal UnitySynchronizationContextRequestExecutor (
            SynchronizationContext? mainThreadSynchronizationContext,
            int mainThreadId)
        {
            this.mainThreadSynchronizationContext = mainThreadSynchronizationContext;
            this.mainThreadId = mainThreadId;

            if (this.mainThreadSynchronizationContext == null)
            {
                // NOTE:
                // Unity batchmode does not guarantee SynchronizationContext.Current during InitializeOnLoad.
                // Keep one update-loop pump attached so background IPC work can still hop onto the main thread.
                EditorApplication.update += ProcessQueueOnEditorUpdate;
            }
        }

        /// <summary> Executes one asynchronous work item on Unity main thread. </summary>
        /// <typeparam name="T"> The work-item result type. </typeparam>
        /// <param name="workItem"> The asynchronous work item to execute. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by connection handling. </param>
        /// <returns> The work-item result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="workItem" /> is <see langword="null" />. </exception>
        /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled. </exception>
        public Task<T> Execute<T> (
            Func<Task<T>> workItem,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            if (Thread.CurrentThread.ManagedThreadId == mainThreadId)
            {
                return workItem();
            }

            var invocation = new MainThreadInvocation<T>(workItem, cancellationToken);
            var shouldScheduleProcessor = false;
            lock (syncRoot)
            {
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

        /// <summary> Processes queued invocations during the Unity editor update loop when SynchronizationContext is unavailable. </summary>
        private void ProcessQueueOnEditorUpdate ()
        {
            if (mainThreadSynchronizationContext != null)
            {
                return;
            }

            ProcessQueueOnMainThread();
        }

        /// <summary> Processes queued main-thread invocations via Unity synchronization context. </summary>
        private void ProcessQueueOnMainThread ()
        {
            IMainThreadInvocation? invocation = null;
            lock (syncRoot)
            {
                if (isRunningInvocation)
                {
                    return;
                }

                if (pendingInvocations.Count == 0)
                {
                    isProcessorActive = false;
                    return;
                }

                invocation = pendingInvocations.Dequeue();
                isRunningInvocation = true;
            }

            _ = RunInvocation(invocation);
        }

        /// <summary> Runs one queued invocation and releases execution gate after completion. </summary>
        /// <param name="invocation"> The queued invocation payload. </param>
        /// <returns> A task that completes after one invocation run finishes. </returns>
        private async Task RunInvocation (IMainThreadInvocation invocation)
        {
            try
            {
                await invocation.Run();
            }
            finally
            {
                var shouldScheduleProcessor = false;
                lock (syncRoot)
                {
                    isRunningInvocation = false;
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
            /// <summary> Runs queued work item on the Unity main thread. </summary>
            Task Run ();

            /// <summary> Completes the invocation as failed when main-thread scheduling cannot proceed. </summary>
            /// <param name="exception"> The scheduling failure. </param>
            void TrySetException (Exception exception);
        }

        /// <summary> Represents one posted main-thread invocation with completion tracking. </summary>
        private sealed class MainThreadInvocation<T> : IMainThreadInvocation
        {
            private readonly Func<Task<T>> workItem;

            private readonly CancellationToken cancellationToken;

            private readonly CancellationTokenRegistration cancellationRegistration;

            /// <summary> Initializes a new instance of the <see cref="MainThreadInvocation" /> class. </summary>
            /// <param name="workItem"> The work item delegate. </param>
            /// <param name="cancellationToken"> The cancellation token for invocation. </param>
            public MainThreadInvocation (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken)
            {
                this.workItem = workItem;
                this.cancellationToken = cancellationToken;
                CompletionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(static state =>
                    {
                        var completionSource = (TaskCompletionSource<T>)state!;
                        completionSource.TrySetCanceled();
                    }, CompletionSource);
                }
            }

            /// <summary> Gets the completion task for this invocation. </summary>
            public Task<T> Completion => CompletionSource.Task;

            /// <summary> Gets the completion source used by this invocation. </summary>
            private TaskCompletionSource<T> CompletionSource { get; }

            /// <summary> Runs posted request delegate and completes invocation result. </summary>
            /// <returns> A task that completes after invocation result has been published. </returns>
            public async Task Run ()
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await workItem();
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
                    cancellationRegistration.Dispose();
                }
            }

            /// <summary> Completes invocation as failed when scheduling on main thread cannot proceed. </summary>
            /// <param name="exception"> The scheduling failure. </param>
            public void TrySetException (Exception exception)
            {
                CompletionSource.TrySetException(exception);
                cancellationRegistration.Dispose();
            }
        }
    }
}
