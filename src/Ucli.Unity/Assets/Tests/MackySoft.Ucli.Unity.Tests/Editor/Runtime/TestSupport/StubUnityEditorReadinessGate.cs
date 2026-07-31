using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

#nullable enable

using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Tests
{
    internal sealed class StubUnityEditorReadinessGate :
        IUnityEditorReadinessGate,
        IUnityEditorAvailabilityObservationSource
    {
        private readonly TaskCompletionSource<UnityEditorExecutionReadinessResult>? completionSource;

        private readonly TaskCompletionSource<bool> waitObserved =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private UnityEditorExecutionReadinessResult currentResult;

        public StubUnityEditorReadinessGate ()
            : this(UnityEditorMode.Batchmode)
        {
        }

        public StubUnityEditorReadinessGate (UnityEditorMode editorMode)
            : this(UnityEditorExecutionReadinessResult.Ready(CreateSnapshot(editorMode, UnityEditorLifecycleState.Ready)), null)
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

        public int CaptureObservationCallCount { get; private set; }

        public int CaptureAvailabilityObservationCallCount { get; private set; }

        public bool? LastFailFast { get; private set; }

        public bool? LastAllowPlayMode { get; private set; }

        public Task WaitObserved => waitObserved.Task;

        public static StubUnityEditorReadinessGate CreatePending ()
        {
            return new StubUnityEditorReadinessGate(
                CreateBlockedResult(
                    UnityEditorMode.Batchmode,
                    UnityEditorLifecycleState.Busy,
                    EditorLifecycleErrorCodes.EditorBusy,
                    "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request."),
                new TaskCompletionSource<UnityEditorExecutionReadinessResult>(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        public UnityEditorRuntimeObservation CaptureObservation ()
        {
            CaptureObservationCallCount++;
            return currentResult.Observation;
        }

        public UnityEditorRuntimeObservation CaptureAvailabilityObservation ()
        {
            CaptureAvailabilityObservationCallCount++;
            return currentResult.Observation;
        }

        public void Release ()
        {
            currentResult = UnityEditorExecutionReadinessResult.Ready(CreateSnapshot(
                currentResult.Observation.State.EditorMode,
                UnityEditorLifecycleState.Ready));
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
            UnityEditorMode editorMode,
            UnityEditorLifecycleState lifecycleState,
            UcliCode errorCode,
            string errorMessage)
        {
            return UnityEditorExecutionReadinessResult.Blocked(
                CreateSnapshot(editorMode, lifecycleState),
                new IpcError(errorCode, errorMessage, null));
        }

        private static UnityEditorRuntimeObservation CreateSnapshot (
            UnityEditorMode editorMode,
            UnityEditorLifecycleState lifecycleState)
        {
            return new UnityEditorRuntimeObservation(
                state: new UnityEditorStateSnapshot(
                    editorMode: editorMode,
                    lifecycleState: lifecycleState,
                    compileState: UnityEditorCompileState.Ready,
                    generations: new UnityEditorGenerationSnapshot(1, 1, 0, 1),
                    playMode: CreatePlayModeSnapshot(lifecycleState)),
                observedAtUtc: DateTimeOffset.UnixEpoch);
        }

        private static UnityEditorPlayModeSnapshot CreatePlayModeSnapshot (UnityEditorLifecycleState lifecycleState)
        {
            var isPlaying = lifecycleState == UnityEditorLifecycleState.PlayMode;
            return new UnityEditorPlayModeSnapshot(
                State: isPlaying ? UnityEditorPlayModeState.Playing : UnityEditorPlayModeState.Stopped,
                Transition: UnityEditorPlayModeTransition.None,
                IsPlaying: isPlaying,
                IsPlayingOrWillChangePlaymode: isPlaying);
        }
    }
}
