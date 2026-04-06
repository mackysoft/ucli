using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;

namespace MackySoft.Ucli.Unity.Tests
{
    internal sealed class StubUnityEditorReadinessGate : IUnityEditorReadinessGate
    {
        private static readonly UnityEditorExecutionReadinessResult ReadyResult =
            UnityEditorExecutionReadinessResult.Ready(CreateSnapshot(IpcEditorLifecycleStateCodec.Ready, null, true));

        private readonly TaskCompletionSource<UnityEditorExecutionReadinessResult>? completionSource;

        private readonly TaskCompletionSource<bool> waitObserved =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private UnityEditorExecutionReadinessResult currentResult;

        public StubUnityEditorReadinessGate ()
            : this(ReadyResult, null)
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

        public bool? LastFailFast { get; private set; }

        public Task WaitObserved => waitObserved.Task;

        public static StubUnityEditorReadinessGate CreatePending ()
        {
            return new StubUnityEditorReadinessGate(
                CreateBlockedResult(
                    IpcEditorLifecycleStateCodec.Busy,
                    IpcEditorBlockingReasonCodec.Busy,
                    IpcErrorCodes.EditorBusy,
                    "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request."),
                new TaskCompletionSource<UnityEditorExecutionReadinessResult>(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        public UnityEditorLifecycleSnapshot CaptureSnapshot ()
        {
            return currentResult.Snapshot;
        }

        public void Release ()
        {
            currentResult = ReadyResult;
            completionSource?.TrySetResult(currentResult);
        }

        public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReady (
            bool failFast,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastFailFast = failFast;
            waitObserved.TrySetResult(true);
            if (completionSource != null && !failFast)
            {
                return completionSource.Task;
            }

            return Task.FromResult(currentResult);
        }

        private static UnityEditorExecutionReadinessResult CreateBlockedResult (
            string lifecycleState,
            string? blockingReason,
            string errorCode,
            string errorMessage)
        {
            return UnityEditorExecutionReadinessResult.Blocked(
                CreateSnapshot(lifecycleState, blockingReason, false),
                new IpcError(errorCode, errorMessage, null));
        }

        private static UnityEditorLifecycleSnapshot CreateSnapshot (
            string lifecycleState,
            string? blockingReason,
            bool canAcceptExecutionRequests)
        {
            return new UnityEditorLifecycleSnapshot(
                Runtime: "batchmode",
                LifecycleState: lifecycleState,
                BlockingReason: blockingReason,
                CompileState: IpcCompileStateCodec.Ready,
                CompileGeneration: "1",
                DomainReloadGeneration: "1",
                CanAcceptExecutionRequests: canAcceptExecutionRequests);
        }
    }
}
