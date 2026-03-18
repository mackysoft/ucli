using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Coordinates readiness waiting for Unity editor compilation and asset-refresh work. </summary>
    internal sealed class UnityEditorReadinessGate : IUnityEditorReadinessGate
    {
        /// <summary> Gets a value indicating whether the editor is ready for editor-mutating requests. </summary>
        internal static bool IsReady => !EditorApplication.isCompiling && !EditorApplication.isUpdating;

        /// <inheritdoc />
        public Task WaitUntilReady (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsReady)
            {
                return Task.CompletedTask;
            }

            var waitState = new ReadinessWaitState(cancellationToken);
            return waitState.AttachAndWait();
        }

        private sealed class ReadinessWaitState
        {
            private readonly CancellationToken cancellationToken;

            private readonly TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private CancellationTokenRegistration cancellationRegistration;

            public ReadinessWaitState (CancellationToken cancellationToken)
            {
                this.cancellationToken = cancellationToken;
            }

            public Task AttachAndWait ()
            {
                EditorApplication.update += OnEditorUpdate;
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(static state =>
                    {
                        var waitState = (ReadinessWaitState)state!;
                        waitState.Cancel();
                    }, this);
                }

                OnEditorUpdate();
                return completionSource.Task;
            }

            private void OnEditorUpdate ()
            {
                if (!IsReady)
                {
                    return;
                }

                Detach();
                completionSource.TrySetResult(true);
            }

            private void Cancel ()
            {
                Detach();
                completionSource.TrySetCanceled(cancellationToken);
            }

            private void Detach ()
            {
                EditorApplication.update -= OnEditorUpdate;
                cancellationRegistration.Dispose();
            }
        }
    }
}