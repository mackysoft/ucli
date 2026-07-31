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
    public sealed class PlayExitLifecycleExecutionHandlerTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("play-exit-lifecycle-execution");

        private static readonly UnityProjectIdentity ProjectIdentity = new(
            ProjectPathTestValues.RepositoryUnityProject,
            ProjectFingerprint,
            "6000.1.4f1");

        private static readonly LifecycleExecutionHostRegistration Host = new(
            new ProcessIdentity(42, 123),
            Guid.Parse("d7c3bbc5-1f84-45de-9513-b29e040205d6"),
            Guid.Parse("c3f18bf6-1f3a-4731-9b46-2538075cfe25"),
            Guid.Parse("c3f18bf6-1f3a-4731-9b46-2538075cfe25"));

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
                var before = Create(CreatePlayingSnapshot(10));
                var after = Create(CreateReadyStoppedSnapshot(11));
                var result = PlayExitTransitionExecutionResult.Success(
                    new PlayLifecycleTransitionResult(
                        PlayLifecycleTransitionCommand.Exit,
                        PlayLifecycleTransitionOutcome.Exited,
                        before,
                        after,
                        Observed: null,
                        ApplicationState: null));
                var handler = new PlayExitLifecycleExecutionHandler(
                    new LateSuccessfulPlayExitLifecycleExecutionProvider(
                        before,
                        result,
                        start.DeadlineUtc.AddMilliseconds(20)),
                    executionStore,
                    new FilePlayExitLifecycleExecutionCheckpointStore(
                        executionStore),
                    NoOpDaemonLogger.Instance);

                var outcome = await handler.ExecuteAsync(start);

                Assert.That(outcome.IsSuccess, Is.False);
                Assert.That(
                    outcome.Error.Code,
                    Is.EqualTo(LifecycleExecutionErrorCodes.DeadlineExceeded));
                Assert.That(
                    outcome.Result.Result,
                    Is.EqualTo(PlayLifecycleTransitionOutcome.Exited));
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
                    Is.EqualTo(PlayLifecycleTransitionOutcome.Exited));
                Assert.That(
                    terminal.TerminalGeneration,
                    Is.EqualTo(after.State.Generations));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenExitSucceeds_PersistsBeforeIssuingAndReconnectsWithoutReissue () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore, playModeGeneration: 10);
                var readinessGate =
                    new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(10));
                var exitRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    readinessGate,
                    exitPlayModeRequester: () =>
                    {
                        var checkpointPath =
                            executionStore.Paths.ResolveCheckpointPath(
                                LifecycleExecutionKind.PlayExit,
                                start.LifecycleExecutionRef.Id,
                                "play-exit-checkpoint.json");
                        Assert.That(
                            GuardedFileExists(checkpointPath),
                            Is.True,
                            "The typed checkpoint must exist before the Unity side effect.");
                        var executionJson = ReadGuardedText(
                            executionStore.Paths.ResolveRecordPath(
                                LifecycleExecutionKind.PlayExit,
                                start.LifecycleExecutionRef.Id));
                        StringAssert.Contains("\"exiting\"", executionJson);
                        exitRequestCount++;
                        readinessGate.Snapshot = CreateReadyStoppedSnapshot(11);
                    });
                var request = CreatePlayExitRequest(start);

                var first = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    request,
                    CancellationToken.None);
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
                    Is.EqualTo(PlayLifecycleTransitionOutcome.Exited));
                Assert.That(
                    firstPayload.LifecycleExecutionRef,
                    Is.EqualTo(secondPayload.LifecycleExecutionRef));
                Assert.That(
                    firstPayload.LifecycleExecutionRef.Lifecycle,
                    Is.EqualTo(ExecutionLifecycle.Terminal));
                Assert.That(exitRequestCount, Is.EqualTo(1));

                var terminalRecord = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(
                    terminalRecord.TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.Completed));
                Assert.That(
                    terminalRecord.Result,
                    Is.EqualTo(
                        PlayExitLifecycleTransitionResult.FromProviderResult(
                            firstPayload.Result)));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenAlreadyStopped_PublishesNotAppliedTerminalWithoutRequest () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    playModeGeneration: 11);
                var exitRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(
                        CreateReadyStoppedSnapshot(11)),
                    exitPlayModeRequester: () => exitRequestCount++);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreatePlayExitRequest(start),
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
                    Is.EqualTo(PlayLifecycleTransitionOutcome.AlreadyExited));
                Assert.That(exitRequestCount, Is.EqualTo(0));
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
        public IEnumerator HandleAsync_WhenPreconditionIsBlocked_PublishesTypedActionFailureWithoutSideEffect () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore, playModeGeneration: 20);
                var readinessGate = new MutableUnityEditorReadinessGate(
                    CreateObservation(
                        UnityEditorMode.Gui,
                        UnityEditorLifecycleState.ModalBlocked,
                        CreatePlayingPlayMode(),
                        playModeGeneration: 20));
                var exitRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    readinessGate,
                    exitPlayModeRequester: () => exitRequestCount++);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreatePlayExitRequest(start),
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    response.Errors[0].Code,
                    Is.EqualTo(PlayModeErrorCodes.PlayModeTransitionBlocked));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcPlayTransitionErrorResponse payload,
                        out _),
                    Is.True);
                Assert.That(
                    payload.Result.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.NotApplied));
                Assert.That(
                    payload.LifecycleExecutionRef.Lifecycle,
                    Is.EqualTo(ExecutionLifecycle.Terminal));
                Assert.That(exitRequestCount, Is.EqualTo(0));
                Assert.That(
                    ReadTerminalRecord(executionStore, start.LifecycleExecutionRef.Id)
                        .TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.ActionFailed));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenUnityRejectsExit_PublishesTypedExplicitFailure () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore, playModeGeneration: 25);
                var readinessGate =
                    new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(25));
                var handler = CreateHandler(
                    executionStore,
                    readinessGate,
                    exitPlayModeRequester: () =>
                        throw new UnityPlayModeTransitionException(
                            "rejected",
                            new InvalidOperationException("provider failure")));

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreatePlayExitRequest(start),
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    response.Errors[0].Code,
                    Is.EqualTo(PlayModeErrorCodes.PlayModeExitRejected));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcPlayTransitionErrorResponse payload,
                        out _),
                    Is.True);
                Assert.That(
                    payload.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.NotApplied));
                Assert.That(payload.Result, Is.Not.Null);
                Assert.That(
                    payload.Result.Result,
                    Is.EqualTo(PlayLifecycleTransitionOutcome.Blocked));
                Assert.That(
                    ReadTerminalRecord(executionStore, start.LifecycleExecutionRef.Id)
                        .TerminalReason,
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
                    playModeGeneration: 26);
                var readinessGate =
                    new MutableUnityEditorReadinessGate(
                        CreatePlayingSnapshot(26));
                var mutationLaneControl =
                    new ImmediateUnityMutationLaneControl();
                var handler = CreateHandler(
                    executionStore,
                    readinessGate,
                    exitPlayModeRequester: () =>
                        throw new InvalidOperationException("adapter bug"),
                    mutationLaneControl: mutationLaneControl);

                InvalidOperationException observedException = null;
                try
                {
                    await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                        handler,
                        CreatePlayExitRequest(start),
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
                    LifecycleExecutionKind.PlayExit,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                Assert.That(execution, Is.Not.Null);
                Assert.That(execution.IsTerminal, Is.False);
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenExecutionDeadlineWasReached_PublishesDeadlineFailureWithoutSideEffect () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    playModeGeneration: 30,
                    deadlineUtc: DateTimeOffset.UtcNow.AddMilliseconds(20));
                await Task.Delay(TimeSpan.FromMilliseconds(40));
                var readinessGate =
                    new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(30));
                var exitRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    readinessGate,
                    exitPlayModeRequester: () => exitRequestCount++);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreatePlayExitRequest(start),
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    response.Errors[0].Code,
                    Is.EqualTo(
                        LifecycleExecutionErrorCodes.DeadlineExceeded));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcPlayTransitionErrorResponse payload,
                        out _),
                    Is.True);
                Assert.That(payload.Result, Is.Null);
                Assert.That(
                    payload.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.NotApplied));
                Assert.That(exitRequestCount, Is.EqualTo(0));
                var terminal = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(
                    terminal.TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.DeadlineExceeded));
                Assert.That(terminal.Result, Is.Null);
                Assert.That(
                    terminal.TerminalGeneration,
                    Is.EqualTo(
                        CreatePlayingSnapshot(30).State.Generations));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenSideEffectWasAdmitted_ObservesCompletionWithoutReissuingExit () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore, playModeGeneration: 40);
                var checkpointStore =
                    new FilePlayExitLifecycleExecutionCheckpointStore(executionStore);
                var before = Create(CreatePlayingSnapshot(40));
                var prepared = (await checkpointStore.CreateOrReadAsync(
                    start.LifecycleExecutionRef.Id,
                    before,
                    CancellationToken.None)).Checkpoint;
                var exitingReference =
                    LifecycleExecutionReferenceFactory.CreateStateProjection(
                        start.LifecycleExecutionRef,
                        ExecutionLifecycle.Active,
                        LifecycleExecutionState.Exiting);
                Assert.That(
                    (await executionStore.TryAcquireSideEffectRightAsync(
                        start.LifecycleExecutionRef,
                        exitingReference,
                        start.Host
                            .CurrentEndpointRegistrationGenerationId,
                        CancellationToken.None)).Outcome,
                    Is.EqualTo(
                        LifecycleExecutionSideEffectRightOutcome.Acquired));
                _ = await checkpointStore.MarkAdmittedAsync(
                    prepared,
                    CancellationToken.None);
                var readinessGate =
                    new MutableUnityEditorReadinessGate(
                        CreateReadyStoppedSnapshot(41));
                var exitRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    readinessGate,
                    checkpointStore,
                    exitPlayModeRequester: () => exitRequestCount++);
                var current = await executionStore.ReadAsync(
                    LifecycleExecutionKind.PlayExit,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        current.Start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);

                Assert.That(exitRequestCount, Is.EqualTo(0));
                var terminal = await executionStore.ReadAsync(
                    LifecycleExecutionKind.PlayExit,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                Assert.That(terminal.IsTerminal, Is.True);
                Assert.That(
                    ReadTerminalRecord(executionStore, start.LifecycleExecutionRef.Id)
                        .Result.Result,
                    Is.EqualTo(PlayLifecycleTransitionOutcome.Exited));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenDeadlineSignalsAtAdmittedProviderEntry_PublishesTypedDeadlineWithoutReissue () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    playModeGeneration: 42,
                    deadlineUtc: DateTimeOffset.UtcNow.AddSeconds(2));
                var checkpointStore =
                    new FilePlayExitLifecycleExecutionCheckpointStore(
                        executionStore);
                var before = Create(CreatePlayingSnapshot(42));
                var prepared = (await checkpointStore.CreateOrReadAsync(
                    start.LifecycleExecutionRef.Id,
                    before,
                    CancellationToken.None)).Checkpoint;
                var exitingReference =
                    LifecycleExecutionReferenceFactory.CreateStateProjection(
                        start.LifecycleExecutionRef,
                        ExecutionLifecycle.Active,
                        LifecycleExecutionState.Exiting);
                Assert.That(
                    (await executionStore.TryAcquireSideEffectRightAsync(
                        start.LifecycleExecutionRef,
                        exitingReference,
                        start.Host
                            .CurrentEndpointRegistrationGenerationId,
                        CancellationToken.None)).Outcome,
                    Is.EqualTo(
                        LifecycleExecutionSideEffectRightOutcome.Acquired));
                _ = await checkpointStore.MarkAdmittedAsync(
                    prepared,
                    CancellationToken.None);
                var provider =
                    new DeadlineAtRecoveryEntryPlayExitLifecycleExecutionProvider(
                        before);
                var handler = new PlayExitLifecycleExecutionHandler(
                    provider,
                    executionStore,
                    checkpointStore,
                    NoOpDaemonLogger.Instance);
                var current = await executionStore.ReadAsync(
                    LifecycleExecutionKind.PlayExit,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                var recoveryRequest = new LifecycleExecutionRecoveryRequest(
                    current.Start,
                    rejectionReason: null,
                    canAttributeCurrentProviderObservation: true);

                await handler.RecoverAsync(
                    recoveryRequest,
                    CancellationToken.None);
                await handler.RecoverAsync(
                    recoveryRequest,
                    CancellationToken.None);

                Assert.That(provider.IssueCallCount, Is.EqualTo(0));
                Assert.That(provider.RecoverCallCount, Is.EqualTo(1));
                var terminal = await executionStore.ReadAsync(
                    LifecycleExecutionKind.PlayExit,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                Assert.That(terminal.IsTerminal, Is.True);
                Assert.That(
                    ReadTerminalRecord(
                        executionStore,
                        start.LifecycleExecutionRef.Id).TerminalReason,
                    Is.EqualTo(
                        LifecycleExecutionTerminalReason.DeadlineExceeded));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenOnlyStartWasRegistered_DoesNotIssueOrTerminalizeAction () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore, playModeGeneration: 50);
                var readinessGate =
                    new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(50));
                var exitRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    readinessGate,
                    exitPlayModeRequester: () => exitRequestCount++);

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);

                Assert.That(exitRequestCount, Is.EqualTo(0));
                var execution = await executionStore.ReadAsync(
                    LifecycleExecutionKind.PlayExit,
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
        public IEnumerator HandleAsync_WhenPayloadIsInvalid_ReturnsInvalidArgumentWithoutCapturingObservation () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var readinessGate =
                    new MutableUnityEditorReadinessGate(CreatePlayingSnapshot(1));
                var handler = CreateHandler(
                    scope.CreateExecutionStore(ProjectFingerprint),
                    readinessGate);
                var request = CreatePlayExitRequest(payload: 123);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    request,
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    response.Errors[0].Code,
                    Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
                Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(0));
            });

        private static async ValueTask<LifecycleExecutionStartBinding> RegisterAsync (
            FileLifecycleExecutionStore executionStore,
            long playModeGeneration,
            DateTimeOffset? deadlineUtc = null)
        {
            var definition =
                new LifecycleExecutionDefinition(LifecycleExecutionKind.PlayExit);
            var result = await executionStore.StartAsync(
                definition,
                Guid.NewGuid(),
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                ProjectIdentity,
                Host,
                new UnityEditorGenerationSnapshot(1, 1, 1, playModeGeneration),
                deadlineUtc ?? DateTimeOffset.UtcNow.AddSeconds(10),
                DateTimeOffset.UtcNow.AddSeconds(-1),
                CancellationToken.None);
            return result.Binding
                ?? throw new AssertionException(
                    $"Play Mode exit registration failed: {result.Outcome}");
        }

        private static PlayExitHandlerFixture CreateHandler (
            FileLifecycleExecutionStore executionStore,
            MutableUnityEditorReadinessGate readinessGate,
            FilePlayExitLifecycleExecutionCheckpointStore checkpointStore = null,
            Func<CancellationToken, Task> editorUpdateAwaiter = null,
            Action exitPlayModeRequester = null,
            IUnityMutationLaneControl mutationLaneControl = null)
        {
            var runner = new PlayExitTransitionRunner(
                new StubServerVersionProvider("1.2.3"),
                readinessGate,
                ProjectIdentity,
                new StubUnityEditorUpdateAwaiter(
                    editorUpdateAwaiter ?? CompleteEditorUpdateAsync),
                new StubUnityPlayModeController(
                    exitPlayModeRequester ?? RequestNoop),
                mutationLaneControl ?? new ImmediateUnityMutationLaneControl());
            var executionHandler = new PlayExitLifecycleExecutionHandler(
                new UnityEditorPlayExitLifecycleExecutionProvider(runner),
                executionStore,
                checkpointStore
                    ?? new FilePlayExitLifecycleExecutionCheckpointStore(
                        executionStore),
                NoOpDaemonLogger.Instance);
            return new PlayExitHandlerFixture(
                new PlayExitUnityIpcMethodHandler(
                    executionHandler,
                    NoOpDaemonLogger.Instance),
                executionHandler);
        }

        private sealed class PlayExitHandlerFixture :
            IUnityIpcMethodHandler
        {
            private readonly PlayExitUnityIpcMethodHandler ipcHandler;
            private readonly PlayExitLifecycleExecutionHandler executionHandler;

            public PlayExitHandlerFixture (
                PlayExitUnityIpcMethodHandler ipcHandler,
                PlayExitLifecycleExecutionHandler executionHandler)
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

        private sealed class LateSuccessfulPlayExitLifecycleExecutionProvider :
            IPlayExitLifecycleExecutionProvider
        {
            private readonly UnityEditorObservation before;
            private readonly PlayExitTransitionExecutionResult result;
            private readonly DateTimeOffset completeAfterUtc;

            public LateSuccessfulPlayExitLifecycleExecutionProvider (
                UnityEditorObservation before,
                PlayExitTransitionExecutionResult result,
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

            public PlayExitTransitionPreparation Prepare (
                CancellationToken executionDeadlineCancellationToken)
            {
                executionDeadlineCancellationToken.ThrowIfCancellationRequested();
                return PlayExitTransitionPreparation.Issue(before);
            }

            public async Task<PlayExitTransitionExecutionResult> IssueAsync (
                UnityEditorObservation durableBefore,
                CancellationToken executionDeadlineCancellationToken)
            {
                Assert.That(durableBefore, Is.EqualTo(before));
                var delay = completeAfterUtc - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay);
                }

                return result;
            }

            public Task<PlayExitTransitionExecutionResult> RecoverAsync (
                UnityEditorObservation durableBefore,
                CancellationToken executionDeadlineCancellationToken)
            {
                throw new AssertionException(
                    "A newly admitted Play Mode exit must use the issue continuation.");
            }
        }

        private sealed class
            DeadlineAtRecoveryEntryPlayExitLifecycleExecutionProvider :
            IPlayExitLifecycleExecutionProvider
        {
            private readonly UnityEditorObservation before;

            public DeadlineAtRecoveryEntryPlayExitLifecycleExecutionProvider (
                UnityEditorObservation before)
            {
                this.before = before;
            }

            public int IssueCallCount { get; private set; }

            public int RecoverCallCount { get; private set; }

            public UnityEditorObservation CaptureObservation ()
            {
                return before;
            }

            public PlayExitTransitionPreparation Prepare (
                CancellationToken executionDeadlineCancellationToken)
            {
                throw new AssertionException(
                    "An admitted Play Mode exit recovery must not prepare again.");
            }

            public Task<PlayExitTransitionExecutionResult> IssueAsync (
                UnityEditorObservation durableBefore,
                CancellationToken executionDeadlineCancellationToken)
            {
                IssueCallCount++;
                throw new AssertionException(
                    "An admitted Play Mode exit recovery must not issue again.");
            }

            public async Task<PlayExitTransitionExecutionResult> RecoverAsync (
                UnityEditorObservation durableBefore,
                CancellationToken executionDeadlineCancellationToken)
            {
                Assert.That(durableBefore, Is.EqualTo(before));
                RecoverCallCount++;
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    executionDeadlineCancellationToken);
                throw new AssertionException(
                    "Play Mode exit recovery must stop at its execution deadline.");
            }
        }

        private static IpcRequestEnvelope CreatePlayExitRequest (
            LifecycleExecutionStartBinding start)
        {
            return CreatePlayExitRequest(new IpcPlayExitRequest(start));
        }

        private static IpcRequestEnvelope CreatePlayExitRequest (object payload)
        {
            return new IpcRequestEnvelope(
                IpcProtocol.CurrentVersion,
                Guid.NewGuid(),
                "session-token",
                TextVocabulary.GetText(UnityIpcMethod.PlayExit),
                IpcPayloadCodec.SerializeToElement(payload),
                "single",
                DateTimeOffset.UtcNow.AddSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
        }

        private static PlayExitLifecycleExecutionTerminalRecord ReadTerminalRecord (
            FileLifecycleExecutionStore executionStore,
            Guid executionId)
        {
            return LifecycleExecutionHandlerTestSupport.ReadTerminalRecord
                <PlayExitLifecycleExecutionTerminalRecord>(
                executionStore,
                LifecycleExecutionKind.PlayExit,
                executionId);
        }

        private static Task CompleteEditorUpdateAsync (
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        private static void RequestNoop ()
        {
        }

        private static UnityEditorRuntimeObservation CreateReadyStoppedSnapshot (
            long generation,
            long compileGeneration = 1)
        {
            return CreateObservation(
                UnityEditorMode.Gui,
                UnityEditorLifecycleState.Ready,
                CreateStoppedPlayMode(),
                generation,
                compileGeneration);
        }

        private static UnityEditorRuntimeObservation CreatePlayingSnapshot (
            long generation,
            long compileGeneration = 1)
        {
            return CreateObservation(
                UnityEditorMode.Gui,
                UnityEditorLifecycleState.PlayMode,
                CreatePlayingPlayMode(),
                generation,
                compileGeneration);
        }

        private static UnityEditorRuntimeObservation CreateObservation (
            UnityEditorMode editorMode,
            UnityEditorLifecycleState lifecycleState,
            UnityEditorPlayModeSnapshot playMode,
            long playModeGeneration,
            long compileGeneration = 1)
        {
            return new UnityEditorRuntimeObservation(
                new UnityEditorStateSnapshot(
                    editorMode,
                    lifecycleState,
                    UnityEditorCompileState.Ready,
                    new UnityEditorGenerationSnapshot(
                        compileGeneration,
                        1,
                        1,
                        playModeGeneration),
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

        private static UnityEditorPlayModeSnapshot CreatePlayingPlayMode ()
        {
            return new UnityEditorPlayModeSnapshot(
                UnityEditorPlayModeState.Playing,
                UnityEditorPlayModeTransition.None,
                IsPlaying: true,
                IsPlayingOrWillChangePlaymode: true);
        }

        private static UnityEditorPlayModeSnapshot CreateStoppedPlayMode ()
        {
            return new UnityEditorPlayModeSnapshot(
                UnityEditorPlayModeState.Stopped,
                UnityEditorPlayModeTransition.None,
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false);
        }

        private sealed class StubUnityEditorUpdateAwaiter :
            IUnityEditorUpdateAwaiter
        {
            private readonly Func<CancellationToken, Task> awaiter;

            public StubUnityEditorUpdateAwaiter (
                Func<CancellationToken, Task> awaiter)
            {
                this.awaiter = awaiter
                    ?? throw new ArgumentNullException(nameof(awaiter));
            }

            public Task WaitForNextUpdateAsync (
                CancellationToken cancellationToken)
            {
                return awaiter(cancellationToken);
            }
        }

        private sealed class StubUnityPlayModeController :
            IUnityPlayModeController
        {
            private readonly Action exitPlayModeRequester;

            public StubUnityPlayModeController (
                Action exitPlayModeRequester)
            {
                this.exitPlayModeRequester = exitPlayModeRequester
                    ?? throw new ArgumentNullException(
                        nameof(exitPlayModeRequester));
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

    }
}
