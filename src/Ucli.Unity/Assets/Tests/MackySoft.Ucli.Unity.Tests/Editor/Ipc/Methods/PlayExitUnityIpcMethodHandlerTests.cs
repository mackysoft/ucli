using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
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
    public sealed class PlayExitUnityIpcMethodHandlerTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("project-fingerprint");

        private static readonly Sha256Digest RequestPayloadHash = Sha256Digest.Parse(
            "cda34040abc54e9b351b66c6ecbc9708cf2c70996b0805553b3854bdce80d94b");

        [Test]
        [Category("Size.Small")]
        public void TryCreateRecoverableRequestPayloadHash_WhenOnlyRequestDeadlineDiffers_ReturnsSameHash ()
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot());
            var handler = CreateHandler(readinessGate);
            var firstRequest = CreatePlayExitRequest(Guid.NewGuid(), new IpcPlayExitRequest(), 1000);
            var secondRequest = CreatePlayExitRequest(Guid.NewGuid(), new IpcPlayExitRequest(), 2000);

            var firstResult = handler.TryCreateRecoverableRequestPayloadHash(
                ValidatedUnityIpcRequestTestFactory.Create(firstRequest),
                out var firstHash,
                out var firstError);
            var secondResult = handler.TryCreateRecoverableRequestPayloadHash(
                ValidatedUnityIpcRequestTestFactory.Create(secondRequest),
                out var secondHash,
                out var secondError);

            Assert.That(firstResult, Is.True, firstError?.Errors[0].Message);
            Assert.That(secondResult, Is.True, secondError?.Errors[0].Message);
            Assert.That(firstHash, Is.Not.Null);
            Assert.That(secondHash, Is.EqualTo(firstHash));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handler_WhenPayloadIsInvalid_ReturnsInvalidArgumentWithoutCapturingSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot());
            var handler = CreateHandler(readinessGate);
            var request = CreatePlayExitRequest(Guid.NewGuid(), 123);

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handler_WhenExitSucceeds_ReturnsExitedTransitionPayload () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: 10));
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => readinessGate.Snapshot = CreateReadyStoppedSnapshot(generation: 11));
            var handler = new PlayExitUnityIpcMethodHandler(runner, NoOpDaemonLogger.Instance);
            var request = CreatePlayExitRequest(Guid.NewGuid(), new IpcPlayExitRequest());

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPlayTransitionResponse payload, out _), Is.True);
            Assert.That(payload.Transition.Transition, Is.EqualTo(IpcPlayTransitionCommand.Exit));
            Assert.That(payload.Transition.Result, Is.EqualTo(IpcPlayTransitionOutcome.Exited));
            Assert.That(payload.Transition.After, Is.Not.Null);
            Assert.That(
                payload.Transition.After!.State.LifecycleState,
                Is.EqualTo(IpcEditorLifecycleState.Ready));
            Assert.That(payload.Transition.After.State.PlayMode!.State, Is.EqualTo(IpcPlayModeState.Stopped));
            Assert.That(payload.Transition.After.State.Generations.PlayModeGeneration, Is.EqualTo(11));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenAlreadyStopped_ReturnsAlreadyExitedWithoutRequestingExit () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateObservation(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleState.Compiling,
                CreateStoppedPlayMode(),
                playModeGeneration: 21));
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => exitRequestCount++);

            var result = await RunExitAsync(runner, 1000, null);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionOutcome.AlreadyExited));
            Assert.That(result.Response.Transition.Before.State.Generations.PlayModeGeneration, Is.EqualTo(21));
            Assert.That(result.Response.Transition.After!.State.Generations.PlayModeGeneration, Is.EqualTo(21));
            Assert.That(exitRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenAlreadyStoppedWithPendingExit_ReturnsRecoveredExited () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateReadyStoppedSnapshot(generation: 22));
            var recoverableStore = new StubRecoverableIpcOperationStore();
            var recoverableContext = CreateRecoverableContext(
                recoverableStore,
                new PlayExitRecoveryPayload(Create(CreatePlayingSnapshot(generation: 21))));
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => exitRequestCount++);

            var result = await RunExitAsync(runner, 1000, recoverableContext);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionOutcome.Exited));
            Assert.That(result.Response.Transition.Before.State.Generations.PlayModeGeneration, Is.EqualTo(21));
            Assert.That(result.Response.Transition.After!.State.Generations.PlayModeGeneration, Is.EqualTo(22));
            Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(0));
            Assert.That(exitRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenNewRecoverableExitStarts_PersistsBeforeSnapshotBeforeRequestingExit () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: 31));
            var recoverableStore = new StubRecoverableIpcOperationStore();
            var recoverableContext = CreateEmptyRecoverableContext(recoverableStore);
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () =>
                {
                    Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(1));
                    exitRequestCount++;
                    readinessGate.Snapshot = CreateReadyStoppedSnapshot(generation: 32);
                });

            var result = await RunExitAsync(runner, 1000, recoverableContext);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionOutcome.Exited));
            Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(1));
            var pendingPayload = recoverableStore.PendingPayload;
            Assert.That(pendingPayload, Is.Not.Null);
            if (pendingPayload == null)
            {
                throw new AssertionException("The pending recovery payload was not captured.");
            }

            Assert.That(pendingPayload.Before.State.Generations.PlayModeGeneration, Is.EqualTo(31));
            Assert.That(exitRequestCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenPendingRecordWriteFails_DoesNotRequestExit () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: 31));
            var recoverableStore = new StubRecoverableIpcOperationStore
            {
                PendingWriteResult = false,
                PendingWriteErrorMessage = "write failed",
            };
            var recoverableContext = CreateEmptyRecoverableContext(recoverableStore);
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => exitRequestCount++);

            var result = await RunExitAsync(runner, 1000, recoverableContext);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeExitRejected));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcApplicationState.NotApplied));
            Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(1));
            Assert.That(exitRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenPendingRecordPayloadIsInvalid_DoesNotRequestExitAgain () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: 31));
            var recoverableStore = new StubRecoverableIpcOperationStore();
            var recoverableContext = CreateRecoverableContext(recoverableStore, new PlayExitRecoveryPayload());
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => exitRequestCount++);

            var result = await RunExitAsync(runner, 1000, recoverableContext);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeStateUnknown));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcApplicationState.Unknown));
            Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(0));
            Assert.That(exitRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenResumingPendingExit_UsesCurrentRequestDeadlineAndResumesObservation () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateExitingSnapshot(generation: 31));
            var recoverableStore = new StubRecoverableIpcOperationStore();
            var recoverableContext = CreateRecoverableContext(
                recoverableStore,
                new PlayExitRecoveryPayload(Create(CreatePlayingSnapshot(generation: 31))));
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                editorUpdateAwaiter: cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    readinessGate.Snapshot = CreateReadyStoppedSnapshot(generation: 32);
                    return Task.CompletedTask;
                },
                exitPlayModeRequester: () => exitRequestCount++);

            var result = await RunExitAsync(runner, 1000, recoverableContext);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionOutcome.Exited));
            Assert.That(result.Response.Transition.Before.State.Generations.PlayModeGeneration, Is.EqualTo(31));
            Assert.That(result.Response.Transition.After!.State.Generations.PlayModeGeneration, Is.EqualTo(32));
            Assert.That(recoverableStore.PendingWriteCallCount, Is.EqualTo(0));
            Assert.That(exitRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenPreconditionIsBlocked_ReturnsBlockedWithoutRequestingExit () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateObservation(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleState.ModalBlocked,
                CreatePlayingPlayMode(),
                playModeGeneration: 41));
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => exitRequestCount++);

            var result = await RunExitAsync(runner, 1000, null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeTransitionBlocked));
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionOutcome.Blocked));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcApplicationState.NotApplied));
            Assert.That(exitRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenPlayModeIsAlreadyChanging_ReturnsAlreadyChangingWithoutRequestingExit () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreateExitingSnapshot(generation: 41));
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => exitRequestCount++);

            var result = await RunExitAsync(runner, 1000, null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeAlreadyChanging));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcApplicationState.NotApplied));
            Assert.That(exitRequestCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenExitRequestIsRejected_ReturnsExitRejected () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: 51));
            var exitRequestCount = 0;
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => exitRequestCount++);

            var result = await RunExitAsync(runner, 1000, null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeExitRejected));
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionOutcome.Blocked));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcApplicationState.NotApplied));
            Assert.That(exitRequestCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenExitCompletesButReadinessIsBlocked_ReturnsAppliedBlocked () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: 61));
            var runner = CreateRunner(
                readinessGate,
                exitPlayModeRequester: () => readinessGate.Snapshot = CreateObservation(
                    DaemonEditorMode.Gui,
                    IpcEditorLifecycleState.SafeMode,
                    CreateStoppedPlayMode(),
                    playModeGeneration: 62));

            var result = await RunExitAsync(runner, 1000, null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeTransitionBlocked));
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionOutcome.Blocked));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcApplicationState.Applied));
            var observed = result.Response.Transition.Observed;
            Assert.That(observed, Is.Not.Null);
            if (observed == null)
            {
                throw new AssertionException("The blocked exit result did not include its observed state.");
            }

            Assert.That(observed.State.PlayMode!.State, Is.EqualTo(IpcPlayModeState.Stopped));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Runner_WhenTransitionTimesOut_ReturnsTimeoutWithObservedSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(generation: 71));
            var runner = CreateRunner(
                readinessGate,
                editorUpdateAwaiter: DelayUntilCanceledAsync);

            var result = await RunExitAsync(runner, 1, null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeTransitionTimeout));
            Assert.That(result.Response.Transition.Result, Is.EqualTo(IpcPlayTransitionOutcome.Timeout));
            Assert.That(result.Response.Transition.ApplicationState, Is.EqualTo(IpcApplicationState.Indeterminate));
            Assert.That(result.Response.Transition.Observed, Is.Not.Null);
            Assert.That(result.Response.Transition.Observed!.State.Generations.PlayModeGeneration, Is.EqualTo(71));
        });

        private static PlayExitUnityIpcMethodHandler CreateHandler (MutableUnityEditorReadinessGate readinessGate)
        {
            return new PlayExitUnityIpcMethodHandler(
                CreateRunner(readinessGate),
                NoOpDaemonLogger.Instance);
        }

        private static PlayExitTransitionRunner CreateRunner (
            MutableUnityEditorReadinessGate readinessGate,
            Func<CancellationToken, Task>? editorUpdateAwaiter = null,
            Action? exitPlayModeRequester = null)
        {
            return new PlayExitTransitionRunner(
                new StubServerVersionProvider("1.2.3"),
                readinessGate,
                new IpcProjectIdentity(ProjectPathTestValues.RepositoryUnityProject, ProjectFingerprint, "6000.1.4f1"),
                new StubUnityEditorUpdateAwaiter(editorUpdateAwaiter ?? CompleteEditorUpdateAsync),
                new StubUnityPlayModeController(exitPlayModeRequester ?? RequestNoop),
                NoOpDaemonLogger.Instance,
                new ImmediateUnityMutationLaneControl());
        }

        private static async Task<PlayExitTransitionExecutionResult> RunExitAsync (
            PlayExitTransitionRunner runner,
            int requestDeadlineRemainingMilliseconds,
            RecoverableIpcOperationContext? recoverableContext)
        {
            var request = CreatePlayExitRequest(
                Guid.NewGuid(),
                new IpcPlayExitRequest(),
                requestDeadlineRemainingMilliseconds);
            using var phaseScope = new IpcRequestPhaseScopeFactory().Create(
                request,
                CancellationToken.None,
                TimeSpan.FromSeconds(1));
            return await runner.ExitAsync(
                recoverableContext,
                phaseScope.ExecutionCancellation);
        }

        private static RecoverableIpcOperationContext CreateRecoverableContext (
            IRecoverableIpcOperationStore store,
            PlayExitRecoveryPayload payload,
            DateTimeOffset? startedAtUtc = null)
        {
            var requestId = Guid.NewGuid();
            return new RecoverableIpcOperationContext(
                store,
                UnityIpcMethod.PlayExit,
                requestId,
                RequestPayloadHash,
                new RecoverableIpcOperationRecord
                {
                    SchemaVersion = 1,
                    ProjectFingerprint = ProjectFingerprint,
                    Method = UnityIpcMethod.PlayExit,
                    RequestId = requestId,
                    State = RecoverableIpcOperationState.Pending,
                    StartedAtUtc = startedAtUtc ?? DateTimeOffset.UtcNow,
                    RecoveryPayload = IpcPayloadCodec.SerializeToElement(payload),
                });
        }

        private static RecoverableIpcOperationContext CreateEmptyRecoverableContext (IRecoverableIpcOperationStore store)
        {
            return new RecoverableIpcOperationContext(
                store,
                UnityIpcMethod.PlayExit,
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

        private static IpcRequestEnvelope CreatePlayExitRequest (
            Guid requestId,
            object payload,
            int requestDeadlineRemainingMilliseconds = 1000)
        {
            return new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: "session-token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.PlayExit),
                payload: IpcPayloadCodec.SerializeToElement(payload),
                responseMode: "single",
                requestDeadlineUtc: DateTimeOffset.UtcNow
                    + TimeSpan.FromMilliseconds(requestDeadlineRemainingMilliseconds),
                requestDeadlineRemainingMilliseconds: requestDeadlineRemainingMilliseconds);
        }

        private static UnityEditorObservation CreateReadyStoppedSnapshot (long generation = 1)
        {
            return CreateObservation(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleState.Ready,
                CreateStoppedPlayMode(),
                generation);
        }

        private static UnityEditorObservation CreatePlayingSnapshot (long generation = 2)
        {
            return CreateObservation(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleState.PlayMode,
                CreatePlayingPlayMode(),
                generation);
        }

        private static UnityEditorObservation CreateExitingSnapshot (long generation = 2)
        {
            return CreateObservation(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleState.PlayMode,
                new IpcPlayModeSnapshot(
                    State: IpcPlayModeState.Exiting,
                    Transition: IpcPlayModeTransition.Exiting,
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true),
                generation);
        }

        private static UnityEditorObservation CreateObservation (
            DaemonEditorMode editorMode,
            IpcEditorLifecycleState lifecycleState,
            IpcPlayModeSnapshot playMode,
            long playModeGeneration)
        {
            return new UnityEditorObservation(
                state: new UnityEditorStateSnapshot(
                    editorMode: editorMode,
                    lifecycleState: lifecycleState,
                    compileState: IpcCompileState.Ready,
                    generations: new IpcUnityGenerationSnapshot(1, 1, 1, playModeGeneration),
                    playMode: playMode),
                observedAtUtc: DateTimeOffset.UnixEpoch);
        }

        private static IpcUnityEditorObservation Create (UnityEditorObservation snapshot)
        {
            return UnityLifecycleResponseFactory.Create(
                new IpcProjectIdentity(ProjectPathTestValues.RepositoryUnityProject, ProjectFingerprint, "6000.1.4f1"),
                "1.2.3",
                snapshot);
        }

        private static IpcPlayModeSnapshot CreatePlayingPlayMode ()
        {
            return new IpcPlayModeSnapshot(
                State: IpcPlayModeState.Playing,
                Transition: IpcPlayModeTransition.None,
                IsPlaying: true,
                IsPlayingOrWillChangePlaymode: true);
        }

        private static IpcPlayModeSnapshot CreateStoppedPlayMode ()
        {
            return new IpcPlayModeSnapshot(
                State: IpcPlayModeState.Stopped,
                Transition: IpcPlayModeTransition.None,
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false);
        }

        private sealed class MutableUnityEditorReadinessGate : IUnityEditorReadinessGate
        {
            public MutableUnityEditorReadinessGate (UnityEditorObservation snapshot)
            {
                Snapshot = snapshot;
            }

            public UnityEditorObservation Snapshot { get; set; }

            public int CaptureObservationCallCount { get; private set; }

            public UnityEditorObservation CaptureObservation ()
            {
                CaptureObservationCallCount++;
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
            private readonly Action exitPlayModeRequester;

            public StubUnityPlayModeController (Action exitPlayModeRequester)
            {
                this.exitPlayModeRequester = exitPlayModeRequester ?? throw new ArgumentNullException(nameof(exitPlayModeRequester));
            }

            public void EnterPlayMode ()
            {
                throw new NotSupportedException();
            }

            public void ExitPlayMode ()
            {
                exitPlayModeRequester();
            }
        }

        private sealed class StubRecoverableIpcOperationStore : IRecoverableIpcOperationStore
        {
            public int PendingWriteCallCount { get; private set; }

            public bool PendingWriteResult { get; set; } = true;

            public string? PendingWriteErrorMessage { get; set; }

            public PlayExitRecoveryPayload? PendingPayload { get; private set; }

            public ValueTask<RecoverableIpcOperationReadResult> ReadAsync (
                UnityIpcMethod method,
                Guid requestId,
                Sha256Digest requestPayloadHash,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<RecoverableIpcOperationReadResult>(
                    RecoverableIpcOperationReadResult.Missing());
            }

            public ValueTask<RecoverableIpcOperationStoreResult> WritePendingAsync (
                UnityIpcMethod method,
                Guid requestId,
                Sha256Digest requestPayloadHash,
                DateTimeOffset startedAtUtc,
                System.Text.Json.JsonElement recoveryPayload,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PendingWriteCallCount++;
                if (!IpcPayloadCodec.TryDeserialize(recoveryPayload, out PlayExitRecoveryPayload pendingPayload, out var readError)
                    || pendingPayload == null)
                {
                    throw new InvalidOperationException($"Pending exit payload was invalid. {readError.Message}");
                }

                PendingPayload = pendingPayload;
                return new ValueTask<RecoverableIpcOperationStoreResult>(PendingWriteResult
                    ? RecoverableIpcOperationStoreResult.Success()
                    : RecoverableIpcOperationStoreResult.Failure(
                        PendingWriteErrorMessage
                        ?? throw new InvalidOperationException("Pending exit write failure did not define an error message.")));
            }

            public ValueTask<RecoverableIpcOperationStoreResult> WriteCompletedAsync (
                UnityIpcMethod method,
                Guid requestId,
                Sha256Digest requestPayloadHash,
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
