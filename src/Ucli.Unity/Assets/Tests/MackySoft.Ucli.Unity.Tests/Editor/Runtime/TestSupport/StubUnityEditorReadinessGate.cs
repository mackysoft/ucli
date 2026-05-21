using System.Threading;
using MackySoft.Ucli.Contracts;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;

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
            : this(UnityEditorExecutionReadinessResult.Ready(CreateSnapshot(editorMode, IpcEditorLifecycleStateCodec.Ready, null, true)), null)
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
                    IpcEditorLifecycleStateCodec.Busy,
                    IpcEditorBlockingReasonCodec.Busy,
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
                IpcEditorLifecycleStateCodec.Ready,
                null,
                true));
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
            string lifecycleState,
            string? blockingReason,
            UcliCode errorCode,
            string errorMessage)
        {
            return UnityEditorExecutionReadinessResult.Blocked(
                CreateSnapshot(editorMode, lifecycleState, blockingReason, false),
                new IpcError(errorCode, errorMessage, null));
        }

        private static UnityEditorLifecycleSnapshot CreateSnapshot (
            DaemonEditorMode editorMode,
            string lifecycleState,
            string? blockingReason,
            bool canAcceptExecutionRequests)
        {
            return new UnityEditorLifecycleSnapshot(
                EditorMode: editorMode,
                LifecycleState: lifecycleState,
                BlockingReason: blockingReason,
                CompileState: IpcCompileStateCodec.Ready,
                CompileGeneration: "1",
                DomainReloadGeneration: "1",
                CanAcceptExecutionRequests: canAcceptExecutionRequests,
                PlayMode: CreatePlayModeSnapshot(lifecycleState));
        }

        private static IpcPlayModeSnapshot CreatePlayModeSnapshot (string lifecycleState)
        {
            var isPlaying = string.Equals(
                lifecycleState,
                IpcEditorLifecycleStateCodec.Playmode,
                System.StringComparison.Ordinal);
            return new IpcPlayModeSnapshot(
                State: isPlaying ? IpcPlayModeStateNames.Playing : IpcPlayModeStateNames.Stopped,
                Transition: IpcPlayModeTransitionNames.None,
                IsPlaying: isPlaying,
                IsPlayingOrWillChangePlaymode: isPlaying,
                Generation: "1");
        }
    }
}
