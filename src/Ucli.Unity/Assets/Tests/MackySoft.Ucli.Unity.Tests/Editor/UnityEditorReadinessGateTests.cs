using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityEditorReadinessGateTests
    {
        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

        [TestCase(IpcEditorLifecycleStateCodec.Starting, true)]
        [TestCase(IpcEditorLifecycleStateCodec.Busy, true)]
        [TestCase(IpcEditorLifecycleStateCodec.Compiling, true)]
        [TestCase(IpcEditorLifecycleStateCodec.DomainReloading, false)]
        [TestCase(IpcEditorLifecycleStateCodec.Playmode, false)]
        [TestCase(IpcEditorLifecycleStateCodec.Ready, false)]
        [TestCase(IpcEditorLifecycleStateCodec.ShuttingDown, false)]
        [Category("Size.Small")]
        public void IsWaitableState_WhenLifecycleStateMatchesPolicy_ReturnsExpectedValue (
            string lifecycleState,
            bool expected)
        {
            var actual = UnityEditorExecutionReadinessPolicy.IsWaitableState(lifecycleState);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(
            IpcEditorLifecycleStateCodec.Starting,
            IpcEditorBlockingReasonCodec.Startup,
            IpcErrorCodes.EditorStarting,
            "Unity editor startup is still in progress. Retry without --failFast or wait until lifecycleState=ready before executing request.")]
        [TestCase(
            IpcEditorLifecycleStateCodec.Busy,
            IpcEditorBlockingReasonCodec.Busy,
            IpcErrorCodes.EditorBusy,
            "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request.")]
        [TestCase(
            IpcEditorLifecycleStateCodec.Compiling,
            IpcEditorBlockingReasonCodec.Compile,
            IpcErrorCodes.EditorCompiling,
            "Unity editor is compiling scripts. Retry without --failFast or wait until lifecycleState=ready before executing request.")]
        [TestCase(
            IpcEditorLifecycleStateCodec.DomainReloading,
            IpcEditorBlockingReasonCodec.DomainReload,
            IpcErrorCodes.EditorDomainReloading,
            "Unity editor is reloading the AppDomain. Retry after lifecycleState=ready before executing request.")]
        [TestCase(
            IpcEditorLifecycleStateCodec.Playmode,
            IpcEditorBlockingReasonCodec.PlayMode,
            IpcErrorCodes.EditorPlaymode,
            "Unity editor is in Play Mode. Exit Play Mode and wait until lifecycleState=ready before executing request.")]
        [TestCase(
            IpcEditorLifecycleStateCodec.ShuttingDown,
            IpcEditorBlockingReasonCodec.Shutdown,
            IpcErrorCodes.EditorShuttingDown,
            "Unity editor is shutting down and cannot accept execution requests.")]
        [Category("Size.Small")]
        public void CreateBlockedResult_WhenLifecycleStateIsMapped_ReturnsExpectedError (
            string lifecycleState,
            string blockingReason,
            string expectedErrorCode,
            string expectedMessage)
        {
            var snapshot = new UnityEditorLifecycleSnapshot(
                Runtime: IpcEditorRuntimeCodec.Batchmode,
                LifecycleState: lifecycleState,
                BlockingReason: blockingReason,
                CompileState: IpcCompileStateCodec.Ready,
                CompileGeneration: "2",
                DomainReloadGeneration: "3",
                CanAcceptExecutionRequests: false);

            var result = UnityEditorExecutionReadinessPolicy.CreateBlockedResult(snapshot);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Snapshot, Is.EqualTo(snapshot));
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(expectedErrorCode));
            Assert.That(result.Error.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureSnapshot_WhenStartupIsPending_DoesNotConsumeStarting ()
        {
            var gate = CreateGate(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true,
                isPlaymodeActive: false,
                out _);

            var first = gate.CaptureSnapshot();
            var second = gate.CaptureSnapshot();

            Assert.That(first.Runtime, Is.EqualTo(IpcEditorRuntimeCodec.Batchmode));
            Assert.That(first.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(first.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.Startup));
            Assert.That(first.CanAcceptExecutionRequests, Is.False);
            Assert.That(first.CompileGeneration, Is.EqualTo("4"));
            Assert.That(first.DomainReloadGeneration, Is.EqualTo("9"));
            Assert.That(second.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(second.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.Startup));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureSnapshot_WhenEditorUpdateCompletesStartup_ReturnsReadySnapshot ()
        {
            var gate = CreateGate(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true,
                isPlaymodeActive: false,
                out var lifecycleTelemetryState);

            var beforeUpdate = gate.CaptureSnapshot();
            lifecycleTelemetryState.ObserveEditorUpdate(
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: false);
            var afterUpdate = gate.CaptureSnapshot();

            Assert.That(beforeUpdate.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(afterUpdate.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(afterUpdate.BlockingReason, Is.Null);
            Assert.That(afterUpdate.CanAcceptExecutionRequests, Is.True);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CaptureSnapshot_WhenPlaymodeIsActive_ReturnsPlaymodeSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 5,
                domainReloadGeneration: 12,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: true,
                out _);

            var snapshot = gate.CaptureSnapshot();

            Assert.That(snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Playmode));
            Assert.That(snapshot.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.PlayMode));
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CaptureSnapshot_WhenEditorIsIdle_ReturnsReadySnapshot () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 5,
                domainReloadGeneration: 12,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                out _);

            var snapshot = gate.CaptureSnapshot();

            Assert.That(snapshot.Runtime, Is.EqualTo(IpcEditorRuntimeCodec.Batchmode));
            Assert.That(snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(snapshot.BlockingReason, Is.Null);
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.True);
            Assert.That(snapshot.CompileGeneration, Is.EqualTo("5"));
            Assert.That(snapshot.DomainReloadGeneration, Is.EqualTo("12"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenFailFastIsDisabled_WaitsUntilEditorBecomesReady () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 6,
                domainReloadGeneration: 13,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: true,
                out _,
                out var activityProbe);

            var resultTask = gate.EnsureExecutionReady(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.False);

            activityProbe.IsUpdating = false;
            await UniTask.Yield();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate default-wait completion",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(result.Snapshot.CanAcceptExecutionRequests, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenStartupIsPending_DoesNotConsumeStartingBeforeNextEditorUpdate () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 7,
                domainReloadGeneration: 14,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true,
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: false,
                out _,
                out _);

            var resultTask = gate.EnsureExecutionReady(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.False);

            await UniTask.Yield();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate startup update completion",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(result.Snapshot.CanAcceptExecutionRequests, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenPlaymodeIsActive_ReturnsBlockedResultWithoutWaiting () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 8,
                domainReloadGeneration: 15,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: true,
                out _);

            var resultTask = gate.EnsureExecutionReady(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.True);
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate playmode blocked result",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Playmode));
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(IpcErrorCodes.EditorPlaymode));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenDomainReloading_ReturnsBlockedResultWithoutWaiting () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 8,
                domainReloadGeneration: 15,
                isDomainReloading: true,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                out _);

            var resultTask = gate.EnsureExecutionReady(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.True);
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate domain reload blocked result",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.DomainReloading));
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(IpcErrorCodes.EditorDomainReloading));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenCanceled_PropagatesCancellation () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 9,
                domainReloadGeneration: 16,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: true,
                out _,
                out _);
            using var cancellationTokenSource = new CancellationTokenSource();

            var resultTask = gate.EnsureExecutionReady(failFast: false, cancellationTokenSource.Token);
            Assert.That(resultTask.IsCompleted, Is.False);

            cancellationTokenSource.Cancel();
            _ = await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await TestAwaiter.WaitAsync(
                    resultTask,
                    "Readiness gate cancellation",
                    AsyncWaitTimeout);
            }, "Readiness gate cancellation result", AsyncWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenAssemblyReloadStartsDuringWait_ReturnsDomainReloadBlockedResult () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 10,
                domainReloadGeneration: 17,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: true,
                out _,
                out _,
                out var waitSignalBus);

            var resultTask = gate.EnsureExecutionReady(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.False);

            waitSignalBus.RaiseBeforeAssemblyReload();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate assembly reload boundary",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.DomainReloading));
            Assert.That(result.Snapshot.CanAcceptExecutionRequests, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(IpcErrorCodes.EditorDomainReloading));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenEditorQuitsDuringWait_ReturnsShuttingDownBlockedResult () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 11,
                domainReloadGeneration: 18,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: true,
                out _,
                out _,
                out var waitSignalBus);

            var resultTask = gate.EnsureExecutionReady(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.False);

            waitSignalBus.RaiseQuitting();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate shutdown boundary",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.ShuttingDown));
            Assert.That(result.Snapshot.CanAcceptExecutionRequests, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(IpcErrorCodes.EditorShuttingDown));
        });

        private static UnityEditorReadinessGate CreateGate (
            int compileGeneration,
            int domainReloadGeneration,
            bool isDomainReloading,
            bool isShuttingDown,
            bool isStartupPending,
            bool isPlaymodeActive,
            out UnityEditorLifecycleTelemetryState lifecycleTelemetryState)
        {
            return CreateGate(
                compileGeneration,
                domainReloadGeneration,
                isDomainReloading,
                isShuttingDown,
                isStartupPending,
                isPlaymodeActive,
                isCompiling: false,
                isUpdating: false,
                out lifecycleTelemetryState,
                out _);
        }

        private static UnityEditorReadinessGate CreateGate (
            int compileGeneration,
            int domainReloadGeneration,
            bool isDomainReloading,
            bool isShuttingDown,
            bool isStartupPending,
            bool isPlaymodeActive,
            bool isCompiling,
            bool isUpdating,
            out UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            out EditorActivityProbe activityProbe)
        {
            return CreateGate(
                compileGeneration,
                domainReloadGeneration,
                isDomainReloading,
                isShuttingDown,
                isStartupPending,
                isPlaymodeActive,
                isCompiling,
                isUpdating,
                out lifecycleTelemetryState,
                out activityProbe,
                out _);
        }

        private static UnityEditorReadinessGate CreateGate (
            int compileGeneration,
            int domainReloadGeneration,
            bool isDomainReloading,
            bool isShuttingDown,
            bool isStartupPending,
            bool isPlaymodeActive,
            bool isCompiling,
            bool isUpdating,
            out UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            out EditorActivityProbe activityProbe,
            out WaitSignalBus waitSignalBus)
        {
            var probe = new EditorActivityProbe
            {
                IsCompiling = isCompiling,
                IsUpdating = isUpdating,
                IsPlaymodeActive = isPlaymodeActive,
            };
            var signalBus = new WaitSignalBus();
            activityProbe = probe;
            waitSignalBus = signalBus;
            lifecycleTelemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration,
                domainReloadGeneration,
                isDomainReloading,
                isShuttingDown,
                isStartupPending);
            return new UnityEditorReadinessGate(
                lifecycleTelemetryState,
                () => probe.IsCompiling,
                () => probe.IsUpdating,
                () => probe.IsPlaymodeActive,
                signalBus.SubscribeBeforeAssemblyReload,
                signalBus.UnsubscribeBeforeAssemblyReload,
                signalBus.SubscribeQuitting,
                signalBus.UnsubscribeQuitting);
        }

        private sealed class EditorActivityProbe
        {
            public bool IsCompiling { get; set; }

            public bool IsUpdating { get; set; }

            public bool IsPlaymodeActive { get; set; }
        }

        private sealed class WaitSignalBus
        {
            private event AssemblyReloadEvents.AssemblyReloadCallback BeforeAssemblyReload;

            private event Action Quitting;

            public void SubscribeBeforeAssemblyReload (AssemblyReloadEvents.AssemblyReloadCallback handler)
            {
                BeforeAssemblyReload += handler;
            }

            public void UnsubscribeBeforeAssemblyReload (AssemblyReloadEvents.AssemblyReloadCallback handler)
            {
                BeforeAssemblyReload -= handler;
            }

            public void SubscribeQuitting (Action handler)
            {
                Quitting += handler;
            }

            public void UnsubscribeQuitting (Action handler)
            {
                Quitting -= handler;
            }

            public void RaiseBeforeAssemblyReload ()
            {
                BeforeAssemblyReload?.Invoke();
            }

            public void RaiseQuitting ()
            {
                Quitting?.Invoke();
            }
        }
    }
}
