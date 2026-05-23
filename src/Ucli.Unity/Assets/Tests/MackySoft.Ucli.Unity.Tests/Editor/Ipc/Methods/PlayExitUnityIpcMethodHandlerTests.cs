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
    public sealed class PlayExitUnityIpcMethodHandlerTests
    {
        private const string RequestPayloadHash = "request-payload-hash";

        [Test]
        [Category("Size.Small")]
        public void TryCreateRecoverableRequestPayloadHash_WhenTimeoutDiffers_ReturnsDifferentHash ()
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot());
            var handler = CreateHandler(readinessGate);
            var firstRequest = CreatePlayExitRequest("req-play-exit-hash-1", new IpcPlayExitRequest
            {
                TimeoutMilliseconds = 1000,
            });
            var secondRequest = CreatePlayExitRequest("req-play-exit-hash-2", new IpcPlayExitRequest
            {
                TimeoutMilliseconds = 2000,
            });

            var firstResult = handler.TryCreateRecoverableRequestPayloadHash(
                firstRequest,
                out var firstHash,
                out var firstError);
            var secondResult = handler.TryCreateRecoverableRequestPayloadHash(
                secondRequest,
                out var secondHash,
                out var secondError);

            Assert.That(firstResult, Is.True, firstError?.Errors[0].Message);
            Assert.That(secondResult, Is.True, secondError?.Errors[0].Message);
            Assert.That(firstHash, Is.Not.Null.And.Not.Empty);
            Assert.That(secondHash, Is.Not.EqualTo(firstHash));
        }

        [Test]
        [Category("Size.Small")]
        public void TryCreateRecoverableRequestPayloadHash_WhenTimeoutIsInvalid_ReturnsInvalidArgument ()
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot());
            var handler = CreateHandler(readinessGate);
            var request = CreatePlayExitRequest("req-play-exit-invalid-timeout", new IpcPlayExitRequest
            {
                TimeoutMilliseconds = 0,
            });

            var result = handler.TryCreateRecoverableRequestPayloadHash(
                request,
                out var requestPayloadHash,
                out var errorResponse);

            Assert.That(result, Is.False);
            Assert.That(requestPayloadHash, Is.Null);
            Assert.That(errorResponse.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(errorResponse.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handler_WhenExitSucceeds_ReturnsExitedTransitionPayload () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: "10"));
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => readinessGate.Snapshot = CreateReadyStoppedSnapshot(generation: "11"));
            var handler = new PlayExitUnityIpcMethodHandler(runner);
            var request = CreatePlayExitRequest("req-play-exit-success", new IpcPlayExitRequest
            {
                TimeoutMilliseconds = 1000,
            });

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPlayTransitionResponse payload, out _), Is.True);
            Assert.That(payload.Transition.Transition, Is.EqualTo(IpcPlayTransitionCommandNames.Exit));
            Assert.That(payload.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Exited));
            Assert.That(payload.Transition.After, Is.Not.Null);
            Assert.That(payload.Transition.After!.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(payload.Transition.After.PlayMode!.State, Is.EqualTo(IpcPlayModeStateNames.Stopped));
            Assert.That(payload.Transition.After.PlayMode.Generation, Is.EqualTo("11"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenAlreadyStopped_ReturnsAlreadyExitedWithoutRequestingExit () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateLifecycleSnapshot(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleStateCodec.Compiling,
                IpcEditorBlockingReasonCodec.Compile,
                canAcceptExecutionRequests: false,
                CreateStoppedPlayMode(generation: "21")));
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => exitRequestCount++);

            var result = await runner.ExitAsync(1000, null, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.AlreadyExited));
            Assert.That(result.Response.Transition.Before.PlayMode!.Generation, Is.EqualTo("21"));
            Assert.That(result.Response.Transition.After!.PlayMode!.Generation, Is.EqualTo("21"));
            Assert.That(exitRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenAlreadyStoppedWithPendingExit_ReturnsRecoveredExited () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot(generation: "22"));
            var recoverableStore = new StubRecoverableIpcOperationStore();
            var recoverableContext = CreateRecoverableContext(
                recoverableStore,
                new PlayExitRecoveryPayload(CreatePlayLifecycleSnapshot(CreatePlayingSnapshot(generation: "21"))));
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => exitRequestCount++);

            var result = await runner.ExitAsync(1000, recoverableContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Exited));
            Assert.That(result.Response.Transition.Before.PlayMode!.Generation, Is.EqualTo("21"));
            Assert.That(result.Response.Transition.After!.PlayMode!.Generation, Is.EqualTo("22"));
            Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(0));
            Assert.That(exitRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenNewRecoverableExitStarts_PersistsBeforeSnapshotBeforeRequestingExit () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: "31"));
            var recoverableStore = new StubRecoverableIpcOperationStore();
            var recoverableContext = CreateEmptyRecoverableContext(recoverableStore);
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () =>
                {
                    Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(1));
                    exitRequestCount++;
                    readinessGate.Snapshot = CreateReadyStoppedSnapshot(generation: "32");
                });

            var result = await runner.ExitAsync(1000, recoverableContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Exited));
            Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(1));
            Assert.That(recoverableStore.PendingPayload, Is.Not.Null);
            Assert.That(recoverableStore.PendingPayload.Before.PlayMode!.Generation, Is.EqualTo("31"));
            Assert.That(exitRequestCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenPreconditionIsBlocked_ReturnsBlockedWithoutRequestingExit () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateLifecycleSnapshot(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleStateCodec.ModalBlocked,
                IpcEditorBlockingReasonCodec.ModalDialog,
                canAcceptExecutionRequests: false,
                CreatePlayingPlayMode(generation: "41")));
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => exitRequestCount++);

            var result = await runner.ExitAsync(1000, null, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeTransitionBlocked));
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Blocked));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcPlayApplicationStateNames.NotApplied));
            Assert.That(exitRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenExitRequestIsRejected_ReturnsExitRejected () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: "51"));
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => exitRequestCount++);

            var result = await runner.ExitAsync(1000, null, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeExitRejected));
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Blocked));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcPlayApplicationStateNames.NotApplied));
            Assert.That(exitRequestCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenExitCompletesButReadinessIsBlocked_ReturnsAppliedBlocked () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: "61"));
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => readinessGate.Snapshot = CreateLifecycleSnapshot(
                    DaemonEditorMode.Gui,
                    IpcEditorLifecycleStateCodec.SafeMode,
                    IpcEditorBlockingReasonCodec.SafeMode,
                    canAcceptExecutionRequests: false,
                    CreateStoppedPlayMode(generation: "62")));

            var result = await runner.ExitAsync(1000, null, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeTransitionBlocked));
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Blocked));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcPlayApplicationStateNames.Applied));
            Assert.That(result.Response.Transition.Observed.PlayMode!.State, Is.EqualTo(IpcPlayModeStateNames.Stopped));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenTransitionTimesOut_ReturnsTimeoutWithObservedSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: "71"));
            var runner = CreateRunner(
                readinessGate,
                editorUpdateAwaiter: DelayUntilCanceledAsync);

            var result = await runner.ExitAsync(1, null, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeTransitionTimeout));
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Timeout));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcPlayApplicationStateNames.Indeterminate));
            Assert.That(result.Response.Transition.Observed, Is.Not.Null);
            Assert.That(result.Response.Transition.Observed!.PlayMode!.Generation, Is.EqualTo("71"));
        });

        private static PlayExitUnityIpcMethodHandler CreateHandler (MutableUnityEditorReadinessGate readinessGate)
        {
            return new PlayExitUnityIpcMethodHandler(CreateRunner(readinessGate));
        }

        private static PlayExitTransitionRunner CreateRunner (
            MutableUnityEditorReadinessGate readinessGate,
            Func<CancellationToken, Task> editorUpdateAwaiter = null,
            Action exitPlayModeRequester = null)
        {
            return new PlayExitTransitionRunner(
                new StubServerVersionProvider("1.2.3"),
                readinessGate,
                new IpcProjectIdentity("/repo/UnityProject", "project-fingerprint", "6000.1.4f1"),
                editorUpdateAwaiter ?? CompleteEditorUpdateAsync,
                exitPlayModeRequester ?? RequestNoop);
        }

        private static RecoverableIpcOperationContext CreateRecoverableContext (
            IRecoverableIpcOperationStore store,
            PlayExitRecoveryPayload payload)
        {
            return new RecoverableIpcOperationContext(
                store,
                IpcMethodNames.PlayExit,
                "req-play-exit-recoverable",
                RequestPayloadHash,
                new RecoverableIpcOperationRecord
                {
                    SchemaVersion = 1,
                    ProjectFingerprint = "project-fingerprint",
                    Method = IpcMethodNames.PlayExit,
                    RequestId = "req-play-exit-recoverable",
                    State = RecoverableIpcOperationState.Pending,
                    StartedAtUtc = DateTimeOffset.UtcNow,
                    RecoveryPayload = IpcPayloadCodec.SerializeToElement(payload),
                });
        }

        private static RecoverableIpcOperationContext CreateEmptyRecoverableContext (IRecoverableIpcOperationStore store)
        {
            return new RecoverableIpcOperationContext(
                store,
                IpcMethodNames.PlayExit,
                "req-play-exit-recoverable",
                RequestPayloadHash,
                null);
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

        private static IpcRequest CreatePlayExitRequest (
            string requestId,
            object payload)
        {
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: "session-token",
                Method: IpcMethodNames.PlayExit,
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
                CreatePlayingPlayMode(generation));
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

        private static IpcPlayLifecycleSnapshot CreatePlayLifecycleSnapshot (UnityEditorLifecycleSnapshot snapshot)
        {
            return UnityLifecycleResponseCodec.CreatePlayLifecycleSnapshot(
                "6000.1.4f1",
                "1.2.3",
                "project-fingerprint",
                snapshot);
        }

        private static IpcPlayModeSnapshot CreatePlayingPlayMode (string generation)
        {
            return new IpcPlayModeSnapshot(
                State: IpcPlayModeStateNames.Playing,
                Transition: IpcPlayModeTransitionNames.None,
                IsPlaying: true,
                IsPlayingOrWillChangePlaymode: true,
                Generation: generation);
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

        private sealed class StubRecoverableIpcOperationStore : IRecoverableIpcOperationStore
        {
            public int PendingWriteCallCount { get; private set; }

            public bool PendingWriteResult { get; set; } = true;

            public string PendingWriteErrorMessage { get; set; }

            public PlayExitRecoveryPayload PendingPayload { get; private set; }

            public bool TryRead (
                string method,
                string requestId,
                string requestPayloadHash,
                out RecoverableIpcOperationRecord record,
                out string errorMessage)
            {
                record = null;
                errorMessage = null;
                return false;
            }

            public bool TryWritePending (
                string method,
                string requestId,
                string requestPayloadHash,
                DateTimeOffset startedAtUtc,
                System.Text.Json.JsonElement recoveryPayload,
                out string errorMessage)
            {
                PendingWriteCallCount++;
                IpcPayloadCodec.TryDeserialize(recoveryPayload, out PlayExitRecoveryPayload pendingPayload, out _);
                PendingPayload = pendingPayload;
                errorMessage = PendingWriteErrorMessage;
                return PendingWriteResult;
            }

            public bool TryWriteCompleted (
                string method,
                string requestId,
                string requestPayloadHash,
                DateTimeOffset startedAtUtc,
                DateTimeOffset completedAtUtc,
                System.Text.Json.JsonElement recoveryPayload,
                IpcResponse response,
                out string errorMessage)
            {
                errorMessage = null;
                return true;
            }

            public bool TryPurgeExpiredRecords (
                DateTimeOffset nowUtc,
                out string errorMessage)
            {
                errorMessage = null;
                return true;
            }
        }
    }
}
