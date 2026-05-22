using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class PlayEnterUnityIpcMethodHandlerTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handler_WhenPayloadIsInvalid_ReturnsInvalidArgumentWithoutCapturingSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot());
            var handler = CreateHandler(readinessGate);
            var request = CreatePlayEnterRequest("req-play-enter-invalid", 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(readinessGate.CaptureSnapshotCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handler_WhenTimeoutIsMissing_ReturnsInvalidArgumentWithoutCapturingSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot());
            var handler = CreateHandler(readinessGate);
            var request = CreatePlayEnterRequest("req-play-enter-missing-timeout", new IpcPlayEnterRequest());

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(readinessGate.CaptureSnapshotCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handler_WhenEnterSucceeds_ReturnsEnteredTransitionPayload () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot(generation: "10"));
            var runner = CreateRunner(
                readinessGate,
                enterPlayModeRequester: () => readinessGate.Snapshot = CreatePlayingSnapshot(generation: "11"));
            var handler = new PlayEnterUnityIpcMethodHandler(runner);
            var request = CreatePlayEnterRequest("req-play-enter-success", new IpcPlayEnterRequest
            {
                TimeoutMilliseconds = 1000,
            });

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPlayTransitionResponse payload, out _), Is.True);
            Assert.That(payload.Transition.Transition, Is.EqualTo(IpcPlayTransitionCommandNames.Enter));
            Assert.That(payload.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Entered));
            Assert.That(payload.Transition.After, Is.Not.Null);
            Assert.That(payload.Transition.After!.PlayMode!.State, Is.EqualTo(IpcPlayModeStateNames.Playing));
            Assert.That(payload.Transition.After.PlayMode.Generation, Is.EqualTo("11"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenAlreadyPlaying_ReturnsAlreadyEnteredWithoutRequestingEnter () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: "21"));
            var enterRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                enterPlayModeRequester: () => enterRequestCount++);

            var result = await runner.EnterAsync(1000, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.AlreadyEntered));
            Assert.That(result.Response.Transition.Before.PlayMode!.Generation, Is.EqualTo("21"));
            Assert.That(result.Response.Transition.After!.PlayMode!.Generation, Is.EqualTo("21"));
            Assert.That(enterRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenPreconditionIsBlocked_ReturnsBlockedWithoutRequestingEnter () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateLifecycleSnapshot(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleStateCodec.Compiling,
                IpcEditorBlockingReasonCodec.Compile,
                canAcceptExecutionRequests: false,
                CreateStoppedPlayMode(generation: "31")));
            var enterRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                enterPlayModeRequester: () => enterRequestCount++);

            var result = await runner.EnterAsync(1000, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeTransitionBlocked));
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Blocked));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcPlayApplicationStateNames.NotApplied));
            Assert.That(enterRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenPlayModeIsAlreadyChanging_ReturnsAlreadyChangingWithoutRequestingEnter () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateLifecycleSnapshot(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleStateCodec.Playmode,
                IpcEditorBlockingReasonCodec.PlayMode,
                canAcceptExecutionRequests: false,
                new IpcPlayModeSnapshot(
                    State: IpcPlayModeStateNames.Entering,
                    Transition: IpcPlayModeTransitionNames.Entering,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: true,
                    Generation: "41")));
            var enterRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                enterPlayModeRequester: () => enterRequestCount++);

            var result = await runner.EnterAsync(1000, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeAlreadyChanging));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcPlayApplicationStateNames.NotApplied));
            Assert.That(enterRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenEnterRequestIsRejected_ReturnsEnterRejected () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot(generation: "51"));
            var enterRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                enterPlayModeRequester: () => enterRequestCount++);

            var result = await runner.EnterAsync(1000, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeEnterRejected));
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Blocked));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcPlayApplicationStateNames.NotApplied));
            Assert.That(enterRequestCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenTransitionTimesOut_ReturnsTimeoutWithObservedSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot(generation: "61"));
            var runner = CreateRunner(
                readinessGate,
                editorUpdateAwaiter: DelayUntilCanceledAsync);

            var result = await runner.EnterAsync(1, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeTransitionTimeout));
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Timeout));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcPlayApplicationStateNames.Indeterminate));
            Assert.That(result.Response.Transition.Observed, Is.Not.Null);
            Assert.That(result.Response.Transition.Observed!.PlayMode!.Generation, Is.EqualTo("61"));
        });

        private static PlayEnterUnityIpcMethodHandler CreateHandler (MutableUnityEditorReadinessGate readinessGate)
        {
            return new PlayEnterUnityIpcMethodHandler(
                new StubServerVersionProvider("1.2.3"),
                readinessGate,
                new IpcProjectIdentity("/repo/UnityProject", "project-fingerprint", "6000.1.4f1"));
        }

        private static PlayEnterTransitionRunner CreateRunner (
            MutableUnityEditorReadinessGate readinessGate,
            Func<CancellationToken, Task> editorUpdateAwaiter = null,
            Action enterPlayModeRequester = null)
        {
            return new PlayEnterTransitionRunner(
                new StubServerVersionProvider("1.2.3"),
                readinessGate,
                new IpcProjectIdentity("/repo/UnityProject", "project-fingerprint", "6000.1.4f1"),
                editorUpdateAwaiter ?? CompleteEditorUpdateAsync,
                enterPlayModeRequester ?? RequestNoop);
        }

        private static Task CompleteEditorUpdateAsync (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        private static Task DelayUntilCanceledAsync (CancellationToken cancellationToken)
        {
            return Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }

        private static void RequestNoop ()
        {
        }

        private static IpcRequest CreatePlayEnterRequest (
            string requestId,
            object payload)
        {
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: "session-token",
                Method: IpcMethodNames.PlayEnter,
                Payload: IpcPayloadCodec.SerializeToElement(payload));
        }

        private static UnityEditorLifecycleSnapshot CreateReadyStoppedSnapshot (string generation = "1")
        {
            return CreateLifecycleSnapshot(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleStateCodec.Ready,
                null,
                canAcceptExecutionRequests: true,
                CreateStoppedPlayMode(generation));
        }

        private static UnityEditorLifecycleSnapshot CreatePlayingSnapshot (string generation = "2")
        {
            return CreateLifecycleSnapshot(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleStateCodec.Playmode,
                IpcEditorBlockingReasonCodec.PlayMode,
                canAcceptExecutionRequests: false,
                new IpcPlayModeSnapshot(
                    State: IpcPlayModeStateNames.Playing,
                    Transition: IpcPlayModeTransitionNames.None,
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true,
                    Generation: generation));
        }

        private static UnityEditorLifecycleSnapshot CreateLifecycleSnapshot (
            DaemonEditorMode editorMode,
            string lifecycleState,
            string? blockingReason,
            bool canAcceptExecutionRequests,
            IpcPlayModeSnapshot playMode)
        {
            return new UnityEditorLifecycleSnapshot(
                EditorMode: editorMode,
                LifecycleState: lifecycleState,
                BlockingReason: blockingReason,
                CompileState: IpcCompileStateCodec.Ready,
                CompileGeneration: "1",
                DomainReloadGeneration: "1",
                CanAcceptExecutionRequests: canAcceptExecutionRequests,
                PlayMode: playMode);
        }

        private static IpcPlayModeSnapshot CreateStoppedPlayMode (string generation)
        {
            return new IpcPlayModeSnapshot(
                State: IpcPlayModeStateNames.Stopped,
                Transition: IpcPlayModeTransitionNames.None,
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false,
                Generation: generation);
        }

        private sealed class MutableUnityEditorReadinessGate : IUnityEditorReadinessGate
        {
            public MutableUnityEditorReadinessGate (UnityEditorLifecycleSnapshot snapshot)
            {
                Snapshot = snapshot;
            }

            public UnityEditorLifecycleSnapshot Snapshot { get; set; }

            public int CaptureSnapshotCallCount { get; private set; }

            public UnityEditorLifecycleSnapshot CaptureSnapshot ()
            {
                CaptureSnapshotCallCount++;
                return Snapshot;
            }

            public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReadyAsync (
                bool failFast,
                CancellationToken cancellationToken = default,
                bool allowPlayMode = false)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(UnityEditorExecutionReadinessResult.Ready(Snapshot));
            }
        }

        private sealed class StubServerVersionProvider : IServerVersionProvider
        {
            private readonly string version;

            public StubServerVersionProvider (string version)
            {
                this.version = version;
            }

            public string GetVersion ()
            {
                return version;
            }
        }
    }
}
