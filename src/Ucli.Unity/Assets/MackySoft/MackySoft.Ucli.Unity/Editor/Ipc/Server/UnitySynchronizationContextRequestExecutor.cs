using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Executes IPC request handlers on Unity main-thread synchronization context. </summary>
    internal sealed class UnitySynchronizationContextRequestExecutor : IUnityMainThreadRequestExecutor
    {
        private readonly object syncRoot = new object();

        private readonly Queue<MainThreadInvocation> pendingInvocations = new Queue<MainThreadInvocation>();

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

        /// <summary> Executes one request-handling delegate on Unity main thread. </summary>
        /// <param name="requestHandler"> The request-handling delegate to execute. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by connection handling. </param>
        /// <returns> The handled IPC response. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestHandler" /> is <see langword="null" />. </exception>
        /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled. </exception>
        public Task<IpcResponse> Execute (
            Func<Task<IpcResponse>> requestHandler,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (requestHandler == null)
            {
                throw new ArgumentNullException(nameof(requestHandler));
            }

            if (Thread.CurrentThread.ManagedThreadId == mainThreadId)
            {
                return requestHandler();
            }

            var invocation = new MainThreadInvocation(requestHandler, cancellationToken);
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
            MainThreadInvocation? invocation = null;
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
        private async Task RunInvocation (MainThreadInvocation invocation)
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
            List<MainThreadInvocation>? failures = null;
            lock (syncRoot)
            {
                if (pendingInvocations.Count > 0)
                {
                    failures = new List<MainThreadInvocation>(pendingInvocations.Count);
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

        /// <summary> Represents one posted main-thread invocation with completion tracking. </summary>
        private sealed class MainThreadInvocation
        {
            private readonly Func<Task<IpcResponse>> requestHandler;

            private readonly CancellationToken cancellationToken;

            private readonly CancellationTokenRegistration cancellationRegistration;

            /// <summary> Initializes a new instance of the <see cref="MainThreadInvocation" /> class. </summary>
            /// <param name="requestHandler"> The request-handling delegate. </param>
            /// <param name="cancellationToken"> The cancellation token for invocation. </param>
            public MainThreadInvocation (
                Func<Task<IpcResponse>> requestHandler,
                CancellationToken cancellationToken)
            {
                this.requestHandler = requestHandler;
                this.cancellationToken = cancellationToken;
                CompletionSource = new TaskCompletionSource<IpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(static state =>
                    {
                        var completionSource = (TaskCompletionSource<IpcResponse>)state!;
                        completionSource.TrySetCanceled();
                    }, CompletionSource);
                }
            }

            /// <summary> Gets the completion task for this invocation. </summary>
            public Task<IpcResponse> Completion => CompletionSource.Task;

            /// <summary> Gets the completion source used by this invocation. </summary>
            private TaskCompletionSource<IpcResponse> CompletionSource { get; }

            /// <summary> Runs posted request delegate and completes invocation result. </summary>
            /// <returns> A task that completes after invocation result has been published. </returns>
            public async Task Run ()
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var response = await requestHandler();
                    CompletionSource.TrySetResult(response);
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