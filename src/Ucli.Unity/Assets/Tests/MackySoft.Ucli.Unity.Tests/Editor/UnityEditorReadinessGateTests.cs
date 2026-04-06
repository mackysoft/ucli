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
        [TestCase(IpcEditorLifecycleStateCodec.DomainReloading, true)]
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
            "Unity editor startup is still in progress. Retry with --waitUntilReady or wait until lifecycleState=ready before executing request.")]
        [TestCase(
            IpcEditorLifecycleStateCodec.Busy,
            IpcEditorBlockingReasonCodec.Busy,
            IpcErrorCodes.EditorBusy,
            "Unity editor is busy with internal work. Retry with --waitUntilReady or wait until lifecycleState=ready before executing request.")]
        [TestCase(
            IpcEditorLifecycleStateCodec.Compiling,
            IpcEditorBlockingReasonCodec.Compile,
            IpcErrorCodes.EditorCompiling,
            "Unity editor is compiling scripts. Retry with --waitUntilReady or wait until lifecycleState=ready before executing request.")]
        [TestCase(
            IpcEditorLifecycleStateCodec.DomainReloading,
            IpcEditorBlockingReasonCodec.DomainReload,
            IpcErrorCodes.EditorDomainReloading,
            "Unity editor is reloading the AppDomain. Retry with --waitUntilReady or wait until lifecycleState=ready before executing request.")]
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
                Runtime: "batchmode",
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
        public void CaptureSnapshot_WhenStartupIsPending_ReturnsBlockedSnapshot ()
        {
            var gate = CreateGate(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true,
                out _);

            var snapshot = gate.CaptureSnapshot();

            Assert.That(snapshot.Runtime, Is.EqualTo("batchmode"));
            Assert.That(snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(snapshot.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.Startup));
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.False);
            Assert.That(snapshot.CompileGeneration, Is.EqualTo("4"));
            Assert.That(snapshot.DomainReloadGeneration, Is.EqualTo("9"));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CaptureSnapshot_WhenEditorIsIdle_ReturnsReadySnapshot () => UniTask.ToCoroutine(async () =>
        {
            await TestAwaiter.WaitAsync(
                UniTask.WaitUntil(static () => !EditorApplication.isCompiling && !EditorApplication.isUpdating).AsTask(),
                "Editor idle before readiness snapshot",
                AsyncWaitTimeout);

            var gate = CreateGate(
                compileGeneration: 5,
                domainReloadGeneration: 12,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                out _);

            var snapshot = gate.CaptureSnapshot();

            Assert.That(snapshot.Runtime, Is.EqualTo("batchmode"));
            Assert.That(snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(snapshot.BlockingReason, Is.Null);
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.True);
            Assert.That(snapshot.CompileGeneration, Is.EqualTo("5"));
            Assert.That(snapshot.DomainReloadGeneration, Is.EqualTo("12"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenWaitUntilReadyIsEnabled_WaitsUntilEditorBecomesReady () => UniTask.ToCoroutine(async () =>
        {
            await TestAwaiter.WaitAsync(
                UniTask.WaitUntil(static () => !EditorApplication.isCompiling && !EditorApplication.isUpdating).AsTask(),
                "Editor idle before readiness wait",
                AsyncWaitTimeout);

            var gate = CreateGate(
                compileGeneration: 6,
                domainReloadGeneration: 13,
                isDomainReloading: true,
                isShuttingDown: false,
                isStartupPending: false,
                out var lifecycleTelemetryState);

            var resultTask = gate.EnsureExecutionReady(waitUntilReady: true);
            Assert.That(resultTask.IsCompleted, Is.False);

            lifecycleTelemetryState.SetDomainReloading(false);
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate wait-until-ready completion",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(result.Snapshot.CanAcceptExecutionRequests, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenStateBecomesNonWaitable_ReturnsBlockedResult () => UniTask.ToCoroutine(async () =>
        {
            await TestAwaiter.WaitAsync(
                UniTask.WaitUntil(static () => !EditorApplication.isCompiling && !EditorApplication.isUpdating).AsTask(),
                "Editor idle before non-waitable transition",
                AsyncWaitTimeout);

            var gate = CreateGate(
                compileGeneration: 7,
                domainReloadGeneration: 14,
                isDomainReloading: true,
                isShuttingDown: false,
                isStartupPending: false,
                out var lifecycleTelemetryState);

            var resultTask = gate.EnsureExecutionReady(waitUntilReady: true);
            Assert.That(resultTask.IsCompleted, Is.False);

            lifecycleTelemetryState.SetDomainReloading(false);
            lifecycleTelemetryState.SetShuttingDown(true);
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate non-waitable transition",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.ShuttingDown));
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(IpcErrorCodes.EditorShuttingDown));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenCanceled_PropagatesCancellation () => UniTask.ToCoroutine(async () =>
        {
            await TestAwaiter.WaitAsync(
                UniTask.WaitUntil(static () => !EditorApplication.isCompiling && !EditorApplication.isUpdating).AsTask(),
                "Editor idle before readiness cancellation",
                AsyncWaitTimeout);

            var gate = CreateGate(
                compileGeneration: 8,
                domainReloadGeneration: 15,
                isDomainReloading: true,
                isShuttingDown: false,
                isStartupPending: false,
                out _);
            using var cancellationTokenSource = new CancellationTokenSource();

            var resultTask = gate.EnsureExecutionReady(waitUntilReady: true, cancellationTokenSource.Token);
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

        private static UnityEditorReadinessGate CreateGate (
            int compileGeneration,
            int domainReloadGeneration,
            bool isDomainReloading,
            bool isShuttingDown,
            bool isStartupPending,
            out UnityEditorLifecycleTelemetryState lifecycleTelemetryState)
        {
            lifecycleTelemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration,
                domainReloadGeneration,
                isDomainReloading,
                isShuttingDown,
                isStartupPending);
            return new UnityEditorReadinessGate(lifecycleTelemetryState);
        }
    }
}
