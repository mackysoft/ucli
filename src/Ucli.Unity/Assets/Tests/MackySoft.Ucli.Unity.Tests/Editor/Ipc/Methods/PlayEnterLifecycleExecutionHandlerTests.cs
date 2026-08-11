using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;
using static MackySoft.Ucli.Unity.Tests.LifecycleExecutionHandlerTestSupport;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class PlayEnterLifecycleExecutionHandlerTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("play-enter-lifecycle-execution");

        private static readonly UnityProjectIdentity ProjectIdentity = new(
            ProjectPathTestValues.RepositoryUnityProject,
            ProjectFingerprint,
            "2023.2.22f1");

        private static readonly LifecycleExecutionHostRegistration Host = new(
            new ProcessIdentity(42, 123),
            Guid.Parse("26eaee2d-dfbb-46c6-b2fd-b8270bdcc16b"),
            Guid.Parse("ff2b56a3-36b8-4382-b408-d1365ca3259b"),
            Guid.Parse("ff2b56a3-36b8-4382-b408-d1365ca3259b"));

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnterAsync_WhenSameExecutionAdmissionWasAlreadyWon_RecoversWithoutSecondRequest () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    playModeGeneration: 10);
                var checkpointStore =
                    new FilePlayEnterLifecycleExecutionCheckpointStore(
                        executionStore);
                var beforeSnapshot = CreateReadyStoppedSnapshot(10);
                var ownerContext = new PlayEnterLifecycleExecutionContext(
                    executionStore,
                    checkpointStore,
                    start.LifecycleExecutionRef.Id,
                    start.Host
                        .CurrentEndpointRegistrationGenerationId,
                    checkpoint: null,
                    enterRecoveryWhenReconnecting: false);
                var enterRequestCount = await ownerContext.TryAdmitSideEffectAsync(
                        Create(beforeSnapshot),
                        CancellationToken.None)
                    ? 1
                    : 0;
                var replayContext = new PlayEnterLifecycleExecutionContext(
                    executionStore,
                    checkpointStore,
                    start.LifecycleExecutionRef.Id,
                    start.Host
                        .CurrentEndpointRegistrationGenerationId,
                    checkpoint: null,
                    enterRecoveryWhenReconnecting: false);
                var runner = new PlayEnterTransitionRunner(
                    new StubServerVersionProvider("1.2.3"),
                    new SequenceUnityEditorReadinessGate(
                        beforeSnapshot,
                        CreatePlayingSnapshot(11)),
                    ProjectIdentity,
                    new StubUnityEditorUpdateAwaiter(),
                    new StubUnityPlayModeController(
                        () => enterRequestCount++),
                    NoOpDaemonLogger.Instance,
                    new ImmediateUnityMutationLaneControl());

                var result = await runner.EnterAsync(
                    replayContext,
                    CancellationToken.None);

                Assert.That(result.IsSuccess, Is.True);
                Assert.That(
                    result.Response.Transition.Result,
                    Is.EqualTo(PlayLifecycleTransitionOutcome.Entered));
                Assert.That(enterRequestCount, Is.EqualTo(1));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenProviderFixesSuccessAfterDeadline_PublishesFailedTerminalWithResult () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    playModeGeneration: 10,
                    deadlineUtc: DateTimeOffset.UtcNow.AddSeconds(1));
                var before = Create(CreateReadyStoppedSnapshot(10));
                var after = Create(CreatePlayingSnapshot(11));
                var result = PlayEnterTransitionExecutionResult.Success(
                    new PlayEnterTransitionExecutionResponse(
                        new PlayLifecycleTransitionResult(
                            PlayLifecycleTransitionCommand.Enter,
                            PlayLifecycleTransitionOutcome.Entered,
                            before,
                            after,
                            Observed: null,
                            ApplicationState: null)));
                var handler = new PlayEnterLifecycleExecutionHandler(
                    new LateSuccessfulPlayEnterLifecycleExecutionProvider(
                        before,
                        result,
                        start.DeadlineUtc.AddMilliseconds(20)),
                    executionStore,
                    new FilePlayEnterLifecycleExecutionCheckpointStore(
                        executionStore),
                    NoOpDaemonLogger.Instance);

                var outcome = await handler.ExecuteAsync(start);

                Assert.That(outcome.IsSuccess, Is.False);
                Assert.That(
                    outcome.Error.Code,
                    Is.EqualTo(LifecycleExecutionErrorCodes.DeadlineExceeded));
                Assert.That(
                    outcome.Result.Result,
                    Is.EqualTo(PlayLifecycleTransitionOutcome.Entered));
                Assert.That(
                    outcome.LifecycleExecutionRef.Lifecycle,
                    Is.EqualTo(ExecutionLifecycle.Terminal));
                Assert.That(
                    outcome.LifecycleExecutionRef.State.Value,
                    Is.EqualTo(TextVocabulary.GetText(
                        LifecycleExecutionState.Failed)));
                var terminal = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(
                    terminal.TerminalReason,
                    Is.EqualTo(
                        LifecycleExecutionTerminalReason.DeadlineExceeded));
                Assert.That(
                    terminal.Result.Result,
                    Is.EqualTo(PlayLifecycleTransitionOutcome.Entered));
                Assert.That(
                    terminal.TerminalGeneration,
                    Is.EqualTo(after.State.Generations));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenEnterSucceeds_ReconnectsWithoutReissue () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore, playModeGeneration: 10);
                var readinessGate =
                    new MutableUnityEditorReadinessGate(
                        CreateReadyStoppedSnapshot(10));
                var enterRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    readinessGate,
                    enterPlayModeRequester: () =>
                    {
                        var checkpointPath =
                            executionStore.Paths.ResolveCheckpointPath(
                                LifecycleExecutionKind.PlayEnter,
                                start.LifecycleExecutionRef.Id,
                                "play-enter-checkpoint.json");
                        Assert.That(
                            GuardedFileExists(checkpointPath),
                            Is.True);
                        StringAssert.Contains(
                            "\"entering\"",
                            ReadGuardedText(
                                executionStore.Paths.ResolveRecordPath(
                                    LifecycleExecutionKind.PlayEnter,
                                    start.LifecycleExecutionRef.Id)));
                        enterRequestCount++;
                        readinessGate.Snapshot = CreatePlayingSnapshot(11);
                    });
                var request = CreatePlayEnterRequest(start);

                var first = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    request,
                    CancellationToken.None);
                var checkpointPath =
                    executionStore.Paths.ResolveCheckpointPath(
                        LifecycleExecutionKind.PlayEnter,
                        start.LifecycleExecutionRef.Id,
                        "play-enter-checkpoint.json");
                WriteGuardedText(checkpointPath, "{");
                var second = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    request,
                    CancellationToken.None);

                Assert.That(first.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(second.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        first.Payload,
                        out IpcPlayTransitionResponse firstPayload,
                        out _),
                    Is.True);
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        second.Payload,
                        out IpcPlayTransitionResponse secondPayload,
                        out _),
                    Is.True);
                Assert.That(
                    firstPayload.Result.Result,
                    Is.EqualTo(PlayLifecycleTransitionOutcome.Entered));
                Assert.That(
                    firstPayload.LifecycleExecutionRef,
                    Is.EqualTo(secondPayload.LifecycleExecutionRef));
                Assert.That(enterRequestCount, Is.EqualTo(1));
                Assert.That(
                    ReadTerminalRecord(
                        executionStore,
                        start.LifecycleExecutionRef.Id).TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.Completed));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenAlreadyPlaying_PublishesNotAppliedTerminalWithoutRequest () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    playModeGeneration: 11);
                var enterRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(
                        CreatePlayingSnapshot(11)),
                    enterPlayModeRequester: () => enterRequestCount++);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreatePlayEnterRequest(start),
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcPlayTransitionResponse payload,
                        out _),
                    Is.True);
                Assert.That(
                    payload.Result.Result,
                    Is.EqualTo(PlayLifecycleTransitionOutcome.AlreadyEntered));
                Assert.That(enterRequestCount, Is.EqualTo(0));
                var terminal = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(
                    terminal.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.NotApplied));
                Assert.That(
                    terminal.TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.Completed));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenUnityRejectsEnter_PublishesTypedActionFailure () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore, playModeGeneration: 20);
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(
                        CreateReadyStoppedSnapshot(20)),
                    enterPlayModeRequester: () =>
                        throw new UnityPlayModeTransitionException(
                            "rejected",
                            new InvalidOperationException("provider failure")));

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreatePlayEnterRequest(start),
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    response.Errors[0].Code,
                    Is.EqualTo(PlayModeErrorCodes.PlayModeEnterRejected));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcPlayTransitionErrorResponse payload,
                        out _),
                    Is.True);
                Assert.That(
                    payload.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.NotApplied));
                Assert.That(
                    payload.Result.Result,
                    Is.EqualTo(PlayLifecycleTransitionOutcome.Blocked));
                Assert.That(
                    ReadTerminalRecord(
                        executionStore,
                        start.LifecycleExecutionRef.Id).TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.ActionFailed));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenPlayModeAdapterHasInternalFailure_PropagatesWithoutTerminalizing () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    playModeGeneration: 21);
                var mutationLaneControl =
                    new ImmediateUnityMutationLaneControl();
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(
                        CreateReadyStoppedSnapshot(21)),
                    enterPlayModeRequester: () =>
                        throw new InvalidOperationException("adapter bug"),
                    mutationLaneControl: mutationLaneControl);

                InvalidOperationException observedException = null;
                try
                {
                    await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                        handler,
                        CreatePlayEnterRequest(start),
                        CancellationToken.None);
                }
                catch (InvalidOperationException exception)
                {
                    observedException = exception;
                }
                Assert.That(observedException, Is.Not.Null);
                await mutationLaneControl.WaitForRetirementAsync();
                Assert.That(mutationLaneControl.BeginCount, Is.EqualTo(1));
                Assert.That(mutationLaneControl.CompleteCount, Is.EqualTo(1));

                var execution = await executionStore.ReadAsync(
                    LifecycleExecutionKind.PlayEnter,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                Assert.That(execution, Is.Not.Null);
                Assert.That(execution.IsTerminal, Is.False);
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenPreparedCheckpointIsRejectedAtDeadline_DoesNotFabricateTransitionResult () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    playModeGeneration: 36,
                    deadlineUtc: DateTimeOffset.UtcNow.AddMilliseconds(-100));
                var checkpointStore =
                    new FilePlayEnterLifecycleExecutionCheckpointStore(
                        executionStore);
                _ = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    Create(CreateReadyStoppedSnapshot(36)),
                    CancellationToken.None);
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(
                        CreateReadyStoppedSnapshot(36)),
                    checkpointStore,
                    enterPlayModeRequester: () =>
                        Assert.Fail(
                            "Deadline recovery must not request Play Mode entry."));

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        start,
                        LifecycleExecutionTerminalReason.DeadlineExceeded,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);

                var terminal = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(terminal.Result, Is.Null);
                Assert.That(
                    terminal.TerminalReason,
                    Is.EqualTo(
                        LifecycleExecutionTerminalReason.DeadlineExceeded));
                Assert.That(
                    terminal.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.NotApplied));
                Assert.That(
                    terminal.TerminalGeneration,
                    Is.EqualTo(
                        CreateReadyStoppedSnapshot(36).State.Generations));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenAdmitted_ObservesCompletionWithoutReissue () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore, playModeGeneration: 40);
                var checkpointStore =
                    new FilePlayEnterLifecycleExecutionCheckpointStore(
                        executionStore);
                var checkpoint = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    Create(CreateReadyStoppedSnapshot(40)),
                    CancellationToken.None);
                var enteringReference =
                    LifecycleExecutionReferenceFactory.CreateStateProjection(
                        start.LifecycleExecutionRef,
                        ExecutionLifecycle.Active,
                        LifecycleExecutionState.Entering);
                Assert.That(
                    (await executionStore.TryAcquireSideEffectRightAsync(
                        start.LifecycleExecutionRef,
                        enteringReference,
                        start.Host
                            .CurrentEndpointRegistrationGenerationId,
                        CancellationToken.None)).Outcome,
                    Is.EqualTo(
                        LifecycleExecutionSideEffectRightOutcome.Acquired));
                _ = await checkpointStore.MarkAdmittedAsync(
                    checkpoint,
                    CancellationToken.None);
                var enterRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(
                        CreatePlayingSnapshot(41)),
                    checkpointStore,
                    enterPlayModeRequester: () => enterRequestCount++);
                var current = await executionStore.ReadAsync(
                    LifecycleExecutionKind.PlayEnter,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        current.Start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);

                Assert.That(enterRequestCount, Is.EqualTo(0));
                Assert.That(
                    ReadTerminalRecord(
                        executionStore,
                        start.LifecycleExecutionRef.Id).Result.Result,
                    Is.EqualTo(PlayLifecycleTransitionOutcome.Entered));
            });

        private static async ValueTask<LifecycleExecutionStartBinding> RegisterAsync (
            FileLifecycleExecutionStore executionStore,
            long playModeGeneration,
            DateTimeOffset? deadlineUtc = null)
        {
            var definition =
                new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.PlayEnter);
            var result = await executionStore.StartAsync(
                definition,
                Guid.NewGuid(),
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                ProjectIdentity,
                Host,
                new UnityEditorGenerationSnapshot(
                    1,
                    1,
                    1,
                    playModeGeneration),
                deadlineUtc ?? DateTimeOffset.UtcNow.AddSeconds(10),
                DateTimeOffset.UtcNow.AddSeconds(-1),
                CancellationToken.None);
            return result.Binding
                ?? throw new AssertionException(
                    $"Play Mode entry registration failed: {result.Outcome}");
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenOnlyStartWasRegisteredBeforeDeadline_RemainsOpenWithoutEnteringPlayMode () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    playModeGeneration: 50);
                var enterRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(
                        CreateReadyStoppedSnapshot(50)),
                    enterPlayModeRequester: () => enterRequestCount++);

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);

                Assert.That(enterRequestCount, Is.EqualTo(0));
                var execution = await executionStore.ReadAsync(
                    LifecycleExecutionKind.PlayEnter,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                Assert.That(execution.IsTerminal, Is.False);
                Assert.That(
                    execution.CurrentReference.State.Value,
                    Is.EqualTo(TextVocabulary.GetText(
                        LifecycleExecutionState.Registered)));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenOnlyStartWasRegisteredAfterDeadline_PublishesNotAppliedTerminalWithoutEnteringPlayMode () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    playModeGeneration: 50,
                    deadlineUtc: DateTimeOffset.UtcNow.AddMilliseconds(-100));
                var enterRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(
                        CreateReadyStoppedSnapshot(50)),
                    enterPlayModeRequester: () => enterRequestCount++);

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);

                Assert.That(enterRequestCount, Is.EqualTo(0));
                Assert.That(
                    await new FilePlayEnterLifecycleExecutionCheckpointStore(
                            executionStore)
                        .ReadAsync(start.LifecycleExecutionRef.Id, CancellationToken.None),
                    Is.Null);
                var terminal = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(
                    terminal.TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.DeadlineExceeded));
                Assert.That(
                    terminal.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.NotApplied));
                Assert.That(terminal.Result, Is.Null);
                Assert.That(terminal.Verdict, Is.Null);
            });

        private static PlayEnterHandlerFixture CreateHandler (
            FileLifecycleExecutionStore executionStore,
            MutableUnityEditorReadinessGate readinessGate,
            FilePlayEnterLifecycleExecutionCheckpointStore checkpointStore = null,
            Action enterPlayModeRequester = null,
            IUnityMutationLaneControl mutationLaneControl = null,
            IDaemonLogger daemonLogger = null)
        {
            daemonLogger ??= NoOpDaemonLogger.Instance;
            var runner = new PlayEnterTransitionRunner(
                new StubServerVersionProvider("1.2.3"),
                readinessGate,
                ProjectIdentity,
                new StubUnityEditorUpdateAwaiter(),
                new StubUnityPlayModeController(
                    enterPlayModeRequester ?? RequestNoop),
                daemonLogger,
                mutationLaneControl ?? new ImmediateUnityMutationLaneControl());
            var executionHandler = new PlayEnterLifecycleExecutionHandler(
                new UnityEditorPlayEnterLifecycleExecutionProvider(runner),
                executionStore,
                checkpointStore
                    ?? new FilePlayEnterLifecycleExecutionCheckpointStore(
                        executionStore),
                daemonLogger);
            return new PlayEnterHandlerFixture(
                new PlayEnterUnityIpcMethodHandler(
                    executionHandler,
                    daemonLogger),
                executionHandler);
        }

        private sealed class PlayEnterHandlerFixture :
            IUnityIpcMethodHandler
        {
            private readonly PlayEnterUnityIpcMethodHandler ipcHandler;
            private readonly PlayEnterLifecycleExecutionHandler executionHandler;

            public PlayEnterHandlerFixture (
                PlayEnterUnityIpcMethodHandler ipcHandler,
                PlayEnterLifecycleExecutionHandler executionHandler)
            {
                this.ipcHandler = ipcHandler;
                this.executionHandler = executionHandler;
            }

            public UnityIpcMethod Method => ipcHandler.Method;

            public ValueTask<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestCancellation cancellation)
            {
                return ipcHandler.HandleAsync(request, cancellation);
            }

            public ValueTask RecoverAsync (
                LifecycleExecutionRecoveryRequest request,
                CancellationToken cancellationToken)
            {
                return executionHandler.RecoverAsync(
                    request,
                    cancellationToken);
            }
        }

        private sealed class LateSuccessfulPlayEnterLifecycleExecutionProvider :
            IPlayEnterLifecycleExecutionProvider
        {
            private readonly UnityEditorObservation before;
            private readonly PlayEnterTransitionExecutionResult result;
            private readonly DateTimeOffset completeAfterUtc;

            public LateSuccessfulPlayEnterLifecycleExecutionProvider (
                UnityEditorObservation before,
                PlayEnterTransitionExecutionResult result,
                DateTimeOffset completeAfterUtc)
            {
                this.before = before;
                this.result = result;
                this.completeAfterUtc = completeAfterUtc;
            }

            public UnityEditorObservation CaptureObservation ()
            {
                return before;
            }

            public async Task<PlayEnterTransitionExecutionResult> EnterAsync (
                IPlayEnterLifecycleExecutionContext executionContext,
                CancellationToken executionDeadlineCancellationToken)
            {
                if (!await executionContext.TryAdmitSideEffectAsync(
                        before,
                        CancellationToken.None))
                {
                    throw new InvalidOperationException(
                        "The delayed provider did not acquire Play Mode entry admission.");
                }
                var delay = completeAfterUtc - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay);
                }

                return result;
            }
        }

        private static IpcRequestEnvelope CreatePlayEnterRequest (
            LifecycleExecutionStartBinding start)
        {
            return new IpcRequestEnvelope(
                IpcProtocol.CurrentVersion,
                Guid.NewGuid(),
                "session-token",
                TextVocabulary.GetText(UnityIpcMethod.PlayEnter),
                IpcPayloadCodec.SerializeToElement(
                    new IpcPlayEnterRequest(start)),
                "single",
                DateTimeOffset.UtcNow.AddSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
        }

        private static PlayEnterLifecycleExecutionTerminalRecord
            ReadTerminalRecord (
                FileLifecycleExecutionStore executionStore,
                Guid executionId)
        {
            return LifecycleExecutionHandlerTestSupport.ReadTerminalRecord
                <PlayEnterLifecycleExecutionTerminalRecord>(
                executionStore,
                LifecycleExecutionKind.PlayEnter,
                executionId);
        }

        private static UnityEditorRuntimeObservation CreateReadyStoppedSnapshot (
            long generation)
        {
            return CreateObservation(
                UnityEditorLifecycleState.Ready,
                new UnityEditorPlayModeSnapshot(
                    UnityEditorPlayModeState.Stopped,
                    UnityEditorPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false),
                generation);
        }

        private static UnityEditorRuntimeObservation CreatePlayingSnapshot (
            long generation,
            long compileGeneration = 1)
        {
            return CreateObservation(
                UnityEditorLifecycleState.PlayMode,
                new UnityEditorPlayModeSnapshot(
                    UnityEditorPlayModeState.Playing,
                    UnityEditorPlayModeTransition.None,
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true),
                generation,
                compileGeneration);
        }

        private static UnityEditorRuntimeObservation CreateObservation (
            UnityEditorLifecycleState lifecycleState,
            UnityEditorPlayModeSnapshot playMode,
            long generation,
            long compileGeneration = 1)
        {
            return new UnityEditorRuntimeObservation(
                new UnityEditorStateSnapshot(
                    UnityEditorMode.Gui,
                    lifecycleState,
                    UnityEditorCompileState.Ready,
                    new UnityEditorGenerationSnapshot(
                        compileGeneration,
                        1,
                        1,
                        generation),
                    playMode),
                DateTimeOffset.UnixEpoch);
        }

        private static UnityEditorObservation Create (
            UnityEditorRuntimeObservation observation)
        {
            return UnityLifecycleResponseFactory.Create(
                ProjectIdentity,
                "1.2.3",
                observation);
        }

        private static void RequestNoop ()
        {
        }

        private sealed class SequenceUnityEditorReadinessGate :
            IUnityEditorReadinessGate
        {
            private readonly UnityEditorRuntimeObservation[] snapshots;

            private int nextSnapshotIndex;

            public SequenceUnityEditorReadinessGate (
                params UnityEditorRuntimeObservation[] snapshots)
            {
                if (snapshots == null || snapshots.Length == 0)
                {
                    throw new ArgumentException(
                        "At least one Unity Editor snapshot is required.",
                        nameof(snapshots));
                }

                this.snapshots = snapshots;
            }

            public UnityEditorRuntimeObservation CaptureObservation ()
            {
                var index = Math.Min(
                    nextSnapshotIndex,
                    snapshots.Length - 1);
                nextSnapshotIndex++;
                return snapshots[index];
            }

            public Task<UnityEditorExecutionReadinessResult>
                EnsureExecutionReadyAsync (
                    bool failFast,
                    CancellationToken cancellationToken = default,
                    bool allowPlayMode = false)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var index = Math.Min(
                    nextSnapshotIndex,
                    snapshots.Length - 1);
                return Task.FromResult(
                    UnityEditorExecutionReadinessResult.Ready(
                        snapshots[index]));
            }
        }

        private sealed class StubUnityEditorUpdateAwaiter :
            IUnityEditorUpdateAwaiter
        {
            public Task WaitForNextUpdateAsync (
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        private sealed class StubUnityPlayModeController :
            IUnityPlayModeController
        {
            private readonly Action enterPlayModeRequester;

            public StubUnityPlayModeController (
                Action enterPlayModeRequester)
            {
                this.enterPlayModeRequester = enterPlayModeRequester
                    ?? throw new ArgumentNullException(
                        nameof(enterPlayModeRequester));
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

    }
}
