using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Executes IPC request handlers on Unity main-thread editor update loop. </summary>
    internal sealed class UnitySynchronizationContextRequestExecutor : IUnityMainThreadRequestExecutor
    {
        private readonly object syncRoot = new object();

        private readonly Queue<MainThreadInvocation> pendingInvocations = new Queue<MainThreadInvocation>();

        private readonly int mainThreadId;

        private bool isUpdateHooked;

        private bool isRunningInvocation;

        /// <summary> Initializes a new instance of the <see cref="UnitySynchronizationContextRequestExecutor" /> class. </summary>
        public UnitySynchronizationContextRequestExecutor ()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
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
            lock (syncRoot)
            {
                pendingInvocations.Enqueue(invocation);
                EnsureUpdateHooked();
            }

            return invocation.Completion;
        }

        /// <summary> Ensures editor-update callback is hooked while there are pending invocations. </summary>
        private void EnsureUpdateHooked ()
        {
            if (isUpdateHooked)
            {
                return;
            }

            EditorApplication.update += OnEditorUpdate;
            isUpdateHooked = true;
        }

        /// <summary> Processes queued main-thread invocations from Unity editor update loop. </summary>
        private void OnEditorUpdate ()
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
                    if (isUpdateHooked)
                    {
                        EditorApplication.update -= OnEditorUpdate;
                        isUpdateHooked = false;
                    }

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
                lock (syncRoot)
                {
                    isRunningInvocation = false;
                    if (pendingInvocations.Count == 0 && isUpdateHooked)
                    {
                        EditorApplication.update -= OnEditorUpdate;
                        isUpdateHooked = false;
                    }
                }
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
        }
    }
}
