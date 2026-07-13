using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    internal sealed class StubUnityEditorReadinessGate : IUnityEditorReadinessGate
    {
        private readonly TaskCompletionSource<UnityEditorExecutionReadinessResult>? completionSource;

        private readonly TaskCompletionSource<bool> waitObserved =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private UnityEditorExecutionReadinessResult currentResult;

        public StubUnityEditorReadinessGate ()
            : this(DaemonEditorMode.Batchmode)
        {
        }

        public StubUnityEditorReadinessGate (DaemonEditorMode editorMode)
            : this(UnityEditorExecutionReadinessResult.Ready(CreateSnapshot(editorMode, IpcEditorLifecycleState.Ready)), null)
        {
        }

        private StubUnityEditorReadinessGate (
            UnityEditorExecutionReadinessResult currentResult,
            TaskCompletionSource<UnityEditorExecutionReadinessResult>? completionSource)
        {
            this.currentResult = currentResult;
            this.completionSource = completionSource;
        }

        public int CallCount { get; private set; }

        public int CaptureSnapshotCallCount { get; private set; }

        public bool? LastFailFast { get; private set; }

        public bool? LastAllowPlayMode { get; private set; }

        public Task WaitObserved => waitObserved.Task;

        public static StubUnityEditorReadinessGate CreatePending ()
        {
            return new StubUnityEditorReadinessGate(
                CreateBlockedResult(
                    DaemonEditorMode.Batchmode,
                    IpcEditorLifecycleState.Busy,
                    EditorLifecycleErrorCodes.EditorBusy,
                    "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request."),
                new TaskCompletionSource<UnityEditorExecutionReadinessResult>(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        public UnityEditorLifecycleSnapshot CaptureSnapshot ()
        {
            CaptureSnapshotCallCount++;
            return currentResult.Snapshot;
        }

        public void Release ()
        {
            currentResult = UnityEditorExecutionReadinessResult.Ready(CreateSnapshot(
                currentResult.Snapshot.EditorMode,
                IpcEditorLifecycleState.Ready));
            completionSource?.TrySetResult(currentResult);
        }

        public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReadyAsync (
            bool failFast,
            CancellationToken cancellationToken = default,
            bool allowPlayMode = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastFailFast = failFast;
            LastAllowPlayMode = allowPlayMode;
            waitObserved.TrySetResult(true);
            if (completionSource != null && !failFast)
            {
                return completionSource.Task;
            }

            return Task.FromResult(currentResult);
        }

        private static UnityEditorExecutionReadinessResult CreateBlockedResult (
            DaemonEditorMode editorMode,
            IpcEditorLifecycleState lifecycleState,
            UcliCode errorCode,
            string errorMessage)
        {
            return UnityEditorExecutionReadinessResult.Blocked(
                CreateSnapshot(editorMode, lifecycleState),
                new IpcError(errorCode, errorMessage, null));
        }

        private static UnityEditorLifecycleSnapshot CreateSnapshot (
            DaemonEditorMode editorMode,
            IpcEditorLifecycleState lifecycleState)
        {
            return new UnityEditorLifecycleSnapshot(
                EditorMode: editorMode,
                LifecycleState: lifecycleState,
                CompileState: IpcCompileState.Ready,
                CompileGeneration: 1,
                DomainReloadGeneration: 1,
                PlayMode: CreatePlayModeSnapshot(lifecycleState));
        }

        private static UnityEditorPlayModeSnapshot CreatePlayModeSnapshot (IpcEditorLifecycleState lifecycleState)
        {
            var isPlaying = lifecycleState == IpcEditorLifecycleState.PlayMode;
            return new UnityEditorPlayModeSnapshot(
                State: isPlaying ? IpcPlayModeState.Playing : IpcPlayModeState.Stopped,
                Transition: IpcPlayModeTransition.None,
                IsPlaying: isPlaying,
                IsPlayingOrWillChangePlaymode: isPlaying,
                Generation: 1);
        }
    }
}
