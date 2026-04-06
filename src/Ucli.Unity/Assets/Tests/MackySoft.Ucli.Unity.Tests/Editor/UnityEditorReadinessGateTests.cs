using System;
using System.Collections;
using System.Reflection;
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
            var actual = InvokePrivateStatic<bool>(
                "IsWaitableState",
                lifecycleState);

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

            var result = InvokePrivateStatic<UnityEditorExecutionReadinessResult>(
                "CreateBlockedResult",
                snapshot);

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
            using var scope = new ReadinessGateStaticStateScope(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true);
            var gate = new UnityEditorReadinessGate();

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

            using var scope = new ReadinessGateStaticStateScope(
                compileGeneration: 5,
                domainReloadGeneration: 12,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false);
            var gate = new UnityEditorReadinessGate();

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

            using var scope = new ReadinessGateStaticStateScope(
                compileGeneration: 6,
                domainReloadGeneration: 13,
                isDomainReloading: true,
                isShuttingDown: false,
                isStartupPending: false);
            var gate = new UnityEditorReadinessGate();

            var resultTask = gate.EnsureExecutionReady(waitUntilReady: true);
            Assert.That(resultTask.IsCompleted, Is.False);

            scope.SetDomainReloading(false);
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

            using var scope = new ReadinessGateStaticStateScope(
                compileGeneration: 7,
                domainReloadGeneration: 14,
                isDomainReloading: true,
                isShuttingDown: false,
                isStartupPending: false);
            var gate = new UnityEditorReadinessGate();

            var resultTask = gate.EnsureExecutionReady(waitUntilReady: true);
            Assert.That(resultTask.IsCompleted, Is.False);

            scope.SetDomainReloading(false);
            scope.SetShuttingDown(true);
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

            using var scope = new ReadinessGateStaticStateScope(
                compileGeneration: 8,
                domainReloadGeneration: 15,
                isDomainReloading: true,
                isShuttingDown: false,
                isStartupPending: false);
            using var cancellationTokenSource = new CancellationTokenSource();
            var gate = new UnityEditorReadinessGate();

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

        private static T InvokePrivateStatic<T> (
            string methodName,
            params object[] arguments)
        {
            var method = typeof(UnityEditorReadinessGate).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Private static method '{methodName}' was not found.");

            var result = method!.Invoke(null, arguments);
            Assert.That(result, Is.Not.Null, $"Private static method '{methodName}' returned null.");
            return (T)result!;
        }

        private sealed class ReadinessGateStaticStateScope : IDisposable
        {
            private readonly int originalCompileGeneration;

            private readonly int originalDomainReloadGeneration;

            private readonly bool originalIsDomainReloading;

            private readonly bool originalIsShuttingDown;

            private readonly bool originalIsStartupPending;

            public ReadinessGateStaticStateScope (
                int compileGeneration,
                int domainReloadGeneration,
                bool isDomainReloading,
                bool isShuttingDown,
                bool isStartupPending)
            {
                originalCompileGeneration = GetPrivateStaticField<int>("compileGeneration");
                originalDomainReloadGeneration = GetPrivateStaticField<int>("domainReloadGeneration");
                originalIsDomainReloading = GetPrivateStaticField<bool>("isDomainReloading");
                originalIsShuttingDown = GetPrivateStaticField<bool>("isShuttingDown");
                originalIsStartupPending = GetPrivateStaticField<bool>("isStartupPending");

                SetPrivateStaticField("compileGeneration", compileGeneration);
                SetPrivateStaticField("domainReloadGeneration", domainReloadGeneration);
                SetPrivateStaticField("isDomainReloading", isDomainReloading);
                SetPrivateStaticField("isShuttingDown", isShuttingDown);
                SetPrivateStaticField("isStartupPending", isStartupPending);
            }

            public void SetShuttingDown (bool value)
            {
                SetPrivateStaticField("isShuttingDown", value);
            }

            public void SetDomainReloading (bool value)
            {
                SetPrivateStaticField("isDomainReloading", value);
            }

            public void SetStartupPending (bool value)
            {
                SetPrivateStaticField("isStartupPending", value);
            }

            public void Dispose ()
            {
                SetPrivateStaticField("compileGeneration", originalCompileGeneration);
                SetPrivateStaticField("domainReloadGeneration", originalDomainReloadGeneration);
                SetPrivateStaticField("isDomainReloading", originalIsDomainReloading);
                SetPrivateStaticField("isShuttingDown", originalIsShuttingDown);
                SetPrivateStaticField("isStartupPending", originalIsStartupPending);
            }

            private static T GetPrivateStaticField<T> (string fieldName)
            {
                var field = GetPrivateStaticFieldInfo(fieldName);
                var value = field.GetValue(null);
                Assert.That(value, Is.Not.Null, $"Private static field '{fieldName}' returned null.");
                return (T)value!;
            }

            private static void SetPrivateStaticField<T> (
                string fieldName,
                T value)
            {
                var field = GetPrivateStaticFieldInfo(fieldName);
                field.SetValue(null, value);
            }

            private static FieldInfo GetPrivateStaticFieldInfo (string fieldName)
            {
                var field = typeof(UnityEditorReadinessGate).GetField(
                    fieldName,
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, $"Private static field '{fieldName}' was not found.");
                return field!;
            }
        }
    }
}
