using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class PlayEnterUnityIpcMethodHandlerTests
    {
        private const string RequestPayloadHash = "request-payload-hash";

        [Test]
        [Category("Size.Small")]
        public void TryCreateRecoverableRequestPayloadHash_WhenOnlyTimeoutDiffers_ReturnsSameHash ()
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot());
            var handler = CreateHandler(readinessGate);
            var firstRequest = CreatePlayEnterRequest(Guid.NewGuid(), new IpcPlayEnterRequest
            {
                TimeoutMilliseconds = 1000,
            });
            var secondRequest = CreatePlayEnterRequest(Guid.NewGuid(), new IpcPlayEnterRequest
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
            Assert.That(secondHash, Is.EqualTo(firstHash));
        }

        [Test]
        [Category("Size.Small")]
        public void TryCreateRecoverableRequestPayloadHash_WhenTimeoutIsInvalid_ReturnsInvalidArgument ()
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot());
            var handler = CreateHandler(readinessGate);
            var request = CreatePlayEnterRequest(Guid.NewGuid(), new IpcPlayEnterRequest
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
        public IEnumerator Handler_WhenPayloadIsInvalid_ReturnsInvalidArgumentWithoutCapturingSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot());
            var handler = CreateHandler(readinessGate);
            var request = CreatePlayEnterRequest(Guid.NewGuid(), 123);

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
            var request = CreatePlayEnterRequest(Guid.NewGuid(), new IpcPlayEnterRequest());

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
            var handler = new PlayEnterUnityIpcMethodHandler(runner, NoOpDaemonLogger.Instance);
            var request = CreatePlayEnterRequest(Guid.NewGuid(), new IpcPlayEnterRequest
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
            Assert.That(payload.Transition.After!.PlayMode!.State, Is.EqualTo("playing"));
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

            var result = await runner.EnterAsync(1000, null, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.AlreadyEntered));
            Assert.That(result.Response.Transition.Before.PlayMode!.Generation, Is.EqualTo("21"));
            Assert.That(result.Response.Transition.After!.PlayMode!.Generation, Is.EqualTo("21"));
            Assert.That(enterRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenAlreadyPlayingWithPendingEnter_ReturnsRecoveredEntered () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: "22"));
            var recoverableStore = new StubRecoverableIpcOperationStore();
            var recoverableContext = CreateRecoverableContext(
                recoverableStore,
                new PlayEnterRecoveryPayload(CreatePlayLifecycleSnapshot(CreateReadyStoppedSnapshot(generation: "21"))));
            var enterRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                enterPlayModeRequester: () => enterRequestCount++);

            var result = await runner.EnterAsync(1000, recoverableContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Entered));
            Assert.That(result.Response.Transition.Before.PlayMode!.Generation, Is.EqualTo("21"));
            Assert.That(result.Response.Transition.After!.PlayMode!.Generation, Is.EqualTo("22"));
            Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(0));
            Assert.That(enterRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenNewRecoverableEnterStarts_PersistsBeforeSnapshotBeforeRequestingEnter () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot(generation: "21"));
            var recoverableStore = new StubRecoverableIpcOperationStore();
            var recoverableContext = CreateEmptyRecoverableContext(recoverableStore);
            var enterRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                enterPlayModeRequester: () =>
                {
                    Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(1));
                    enterRequestCount++;
                    readinessGate.Snapshot = CreatePlayingSnapshot(generation: "22");
                });

            var result = await runner.EnterAsync(1000, recoverableContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Entered));
            Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(1));
            var pendingPayload = recoverableStore.PendingPayload
                ?? throw new InvalidOperationException("Recoverable store did not capture the pending enter payload.");
            var pendingPlayMode = pendingPayload.Before.PlayMode
                ?? throw new InvalidOperationException("Pending enter payload did not include Play Mode state.");
            Assert.That(pendingPlayMode.Generation, Is.EqualTo("21"));
            Assert.That(enterRequestCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenPendingRecordWriteFails_DoesNotRequestEnter () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot(generation: "21"));
            var recoverableStore = new StubRecoverableIpcOperationStore
            {
                PendingWriteResult = false,
                PendingWriteErrorMessage = "write failed",
            };
            var recoverableContext = CreateEmptyRecoverableContext(recoverableStore);
            var enterRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                enterPlayModeRequester: () => enterRequestCount++);

            var result = await runner.EnterAsync(1000, recoverableContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeEnterRejected));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcPlayApplicationStateNames.NotApplied));
            Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(1));
            Assert.That(enterRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenPendingRecordPayloadIsInvalid_DoesNotRequestEnterAgain () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot(generation: "21"));
            var recoverableStore = new StubRecoverableIpcOperationStore();
            var recoverableContext = CreateRecoverableContext(recoverableStore, new PlayEnterRecoveryPayload());
            var enterRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                enterPlayModeRequester: () => enterRequestCount++);

            var result = await runner.EnterAsync(1000, recoverableContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeStateUnknown));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcPlayApplicationStateNames.Unknown));
            Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(0));
            Assert.That(enterRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenPendingEnterIsStillChanging_ResumesObservationWithoutRequestingEnterAgain () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateEnteringSnapshot(generation: "21"));
            var recoverableStore = new StubRecoverableIpcOperationStore();
            var recoverableContext = CreateRecoverableContext(
                recoverableStore,
                new PlayEnterRecoveryPayload(CreatePlayLifecycleSnapshot(CreateReadyStoppedSnapshot(generation: "21"))));
            var enterRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                editorUpdateAwaiter: cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    readinessGate.Snapshot = CreatePlayingSnapshot(generation: "22");
                    return Task.CompletedTask;
                },
                enterPlayModeRequester: () => enterRequestCount++);

            var result = await runner.EnterAsync(1000, recoverableContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionResultNames.Entered));
            Assert.That(result.Response.Transition.Before.PlayMode!.Generation, Is.EqualTo("21"));
            Assert.That(result.Response.Transition.After!.PlayMode!.Generation, Is.EqualTo("22"));
            Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(0));
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

            var result = await runner.EnterAsync(1000, null, CancellationToken.None);

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
                    State: "entering",
                    Transition: "entering",
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: true,
                    Generation: "41")));
            var enterRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                enterPlayModeRequester: () => enterRequestCount++);

            var result = await runner.EnterAsync(1000, null, CancellationToken.None);

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

            var result = await runner.EnterAsync(1000, null, CancellationToken.None);

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

            var result = await runner.EnterAsync(1, null, CancellationToken.None);

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
                CreateRunner(readinessGate),
                NoOpDaemonLogger.Instance);
        }

        private static PlayEnterTransitionRunner CreateRunner (
            MutableUnityEditorReadinessGate readinessGate,
            Func<CancellationToken, Task>? editorUpdateAwaiter = null,
            Action? enterPlayModeRequester = null)
        {
            return new PlayEnterTransitionRunner(
                new StubServerVersionProvider("1.2.3"),
                readinessGate,
                new IpcProjectIdentity("/repo/UnityProject", "project-fingerprint", "6000.1.4f1"),
                new StubUnityEditorUpdateAwaiter(editorUpdateAwaiter ?? CompleteEditorUpdateAsync),
                new StubUnityPlayModeController(enterPlayModeRequester ?? RequestNoop),
                NoOpDaemonLogger.Instance);
        }

        private static RecoverableIpcOperationContext CreateRecoverableContext (
            IRecoverableIpcOperationStore store,
            PlayEnterRecoveryPayload payload)
        {
            var requestId = Guid.NewGuid();
            return new RecoverableIpcOperationContext(
                store,
                UnityIpcMethod.PlayEnter,
                requestId,
                RequestPayloadHash,
                new RecoverableIpcOperationRecord
                {
                    SchemaVersion = 1,
                    ProjectFingerprint = "project-fingerprint",
                    Method = ContractLiteralCodec.ToValue(UnityIpcMethod.PlayEnter),
                    RequestId = requestId,
                    State = RecoverableIpcOperationState.Pending,
                    StartedAtUtc = DateTimeOffset.UtcNow,
                    RecoveryPayload = IpcPayloadCodec.SerializeToElement(payload),
                });
        }

        private static RecoverableIpcOperationContext CreateEmptyRecoverableContext (IRecoverableIpcOperationStore store)
        {
            return new RecoverableIpcOperationContext(
                store,
                UnityIpcMethod.PlayEnter,
                Guid.NewGuid(),
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

        private static IpcRequest CreatePlayEnterRequest (
            Guid requestId,
            object payload)
        {
            return new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: "session-token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.PlayEnter),
                payload: IpcPayloadCodec.SerializeToElement(payload),
                responseMode: "single");
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
                    State: "playing",
                    Transition: "none",
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true,
                    Generation: generation));
        }

        private static UnityEditorLifecycleSnapshot CreateEnteringSnapshot (string generation = "2")
        {
            return CreateLifecycleSnapshot(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleStateCodec.Playmode,
                IpcEditorBlockingReasonCodec.PlayMode,
                canAcceptExecutionRequests: false,
                new IpcPlayModeSnapshot(
                    State: "entering",
                    Transition: "entering",
                    IsPlaying: false,
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

        private static IpcPlayLifecycleSnapshot CreatePlayLifecycleSnapshot (UnityEditorLifecycleSnapshot snapshot)
        {
            return UnityLifecycleResponseCodec.CreatePlayLifecycleSnapshot(
                "6000.1.4f1",
                "1.2.3",
                "project-fingerprint",
                snapshot);
        }

        private static IpcPlayModeSnapshot CreateStoppedPlayMode (string generation)
        {
            return new IpcPlayModeSnapshot(
                State: "stopped",
                Transition: "none",
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

        private sealed class StubUnityEditorUpdateAwaiter : IUnityEditorUpdateAwaiter
        {
            private readonly Func<CancellationToken, Task> awaiter;

            public StubUnityEditorUpdateAwaiter (Func<CancellationToken, Task> awaiter)
            {
                this.awaiter = awaiter ?? throw new ArgumentNullException(nameof(awaiter));
            }

            public Task WaitForNextUpdateAsync (CancellationToken cancellationToken)
            {
                return awaiter(cancellationToken);
            }
        }

        private sealed class StubUnityPlayModeController : IUnityPlayModeController
        {
            private readonly Action enterPlayModeRequester;

            public StubUnityPlayModeController (Action enterPlayModeRequester)
            {
                this.enterPlayModeRequester = enterPlayModeRequester ?? throw new ArgumentNullException(nameof(enterPlayModeRequester));
            }

            public void EnterPlayMode ()
            {
                enterPlayModeRequester();
            }

            public void ExitPlayMode ()
            {
                throw new NotSupportedException();
            }
        }

        private sealed class StubRecoverableIpcOperationStore : IRecoverableIpcOperationStore
        {
            public int PendingWriteCallCount { get; private set; }

            public bool PendingWriteResult { get; set; } = true;

            public string? PendingWriteErrorMessage { get; set; }

            public PlayEnterRecoveryPayload? PendingPayload { get; private set; }

            public ValueTask<RecoverableIpcOperationReadResult> ReadAsync (
                UnityIpcMethod method,
                Guid requestId,
                string requestPayloadHash,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<RecoverableIpcOperationReadResult>(
                    RecoverableIpcOperationReadResult.Missing());
            }

            public ValueTask<RecoverableIpcOperationStoreResult> WritePendingAsync (
                UnityIpcMethod method,
                Guid requestId,
                string requestPayloadHash,
                DateTimeOffset startedAtUtc,
                System.Text.Json.JsonElement recoveryPayload,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PendingWriteCallCount++;
                if (!IpcPayloadCodec.TryDeserialize(recoveryPayload, out PlayEnterRecoveryPayload pendingPayload, out var readError)
                    || pendingPayload == null)
                {
                    throw new InvalidOperationException($"Pending enter payload was invalid. {readError.Message}");
                }

                PendingPayload = pendingPayload;
                return new ValueTask<RecoverableIpcOperationStoreResult>(PendingWriteResult
                    ? RecoverableIpcOperationStoreResult.Success()
                    : RecoverableIpcOperationStoreResult.Failure(
                        PendingWriteErrorMessage
                        ?? throw new InvalidOperationException("Pending enter write failure did not define an error message.")));
            }

            public ValueTask<RecoverableIpcOperationStoreResult> WriteCompletedAsync (
                UnityIpcMethod method,
                Guid requestId,
                string requestPayloadHash,
                DateTimeOffset startedAtUtc,
                DateTimeOffset completedAtUtc,
                System.Text.Json.JsonElement recoveryPayload,
                IpcResponse response,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<RecoverableIpcOperationStoreResult>(
                    RecoverableIpcOperationStoreResult.Success());
            }

            public string? ConsumeMaintenanceFailure ()
            {
                return null;
            }
        }
    }
}
