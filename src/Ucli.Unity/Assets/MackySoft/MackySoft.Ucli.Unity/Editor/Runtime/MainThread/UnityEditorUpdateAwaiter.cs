using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Waits for the next Unity Editor update callback. </summary>
    internal static class UnityEditorUpdateAwaiter
    {
        /// <summary> Completes after one <see cref="EditorApplication.update" /> callback. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
        /// <returns> A task that completes on the next Editor update. </returns>
        public static Task WaitForNextUpdateAsync (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new EditorUpdateWaitState(cancellationToken).Attach();
        }

        private sealed class EditorUpdateWaitState
        {
            private readonly CancellationToken cancellationToken;

            private readonly SynchronizationContext synchronizationContext;

            private readonly TaskCompletionSource<object> completionSource =
                new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            private CancellationTokenRegistration cancellationRegistration;

            private int detached;

            public EditorUpdateWaitState (CancellationToken cancellationToken)
            {
                this.cancellationToken = cancellationToken;
                synchronizationContext = SynchronizationContext.Current;
            }

            public Task Attach ()
            {
                EditorApplication.update += CompleteOnEditorUpdate;
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(static state =>
                    {
                        var waitState = (EditorUpdateWaitState)state;
                        waitState.Cancel();
                    }, this);
                    if (Volatile.Read(ref detached) != 0)
                    {
                        cancellationRegistration.Dispose();
                    }
                }

                return completionSource.Task;
            }

            private void CompleteOnEditorUpdate ()
            {
                DetachOnMainThread();
                completionSource.TrySetResult(null);
            }

            private void Cancel ()
            {
                completionSource.TrySetCanceled(cancellationToken);
                if (synchronizationContext == null)
                {
                    return;
                }

                if (SynchronizationContext.Current == synchronizationContext)
                {
                    DetachOnMainThread();
                    return;
                }

                synchronizationContext.Post(static state =>
                {
                    var waitState = (EditorUpdateWaitState)state;
                    waitState.DetachOnMainThread();
                }, this);
            }

            private void DetachOnMainThread ()
            {
                if (Interlocked.Exchange(ref detached, 1) != 0)
                {
                    return;
                }

                EditorApplication.update -= CompleteOnEditorUpdate;
                cancellationRegistration.Dispose();
            }
        }
    }
}
