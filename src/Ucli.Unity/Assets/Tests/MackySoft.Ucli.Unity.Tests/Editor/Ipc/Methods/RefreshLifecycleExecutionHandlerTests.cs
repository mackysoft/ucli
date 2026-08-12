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
    public sealed class RefreshLifecycleExecutionHandlerTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("refresh-lifecycle-execution");

        private static readonly UnityProjectIdentity ProjectIdentity = new(
            ProjectPathTestValues.RepositoryUnityProject,
            ProjectFingerprint,
            "2023.2.22f1");

        private static readonly LifecycleExecutionHostRegistration Host = new(
            new ProcessIdentity(42, 123),
            Guid.Parse("9a31a97b-e5d2-4617-9134-b1164cf9e35c"),
            Guid.Parse("9df2036e-c4e9-438f-9a78-f897331583ee"),
            Guid.Parse("9df2036e-c4e9-438f-9a78-f897331583ee"));

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenProviderFixesResultAfterDeadline_PublishesResultOnlyInFailedTerminalRecord () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var timeSource = new ManualLifecycleExecutionTimeSource(
                    new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero));
                var start = await RegisterAsync(
                    executionStore,
                    timeSource.UtcNow.AddSeconds(1),
                    timeSource.UtcNow);
                var finalObservationEntered = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseFinalObservation = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var provider =
                    new RecordingRefreshLifecycleExecutionProvider(
                        CreateReadySnapshot(),
                        finalObservationEntered,
                        releaseFinalObservation.Task);
                var executionHandler = new RefreshLifecycleExecutionHandler(
                    provider,
                    NoOpDaemonLogger.Instance,
                    executionStore,
                    new FileRefreshLifecycleExecutionCheckpointStore(
                        executionStore),
                    timeSource);
                var handler = new RefreshUnityIpcMethodHandler(
                    executionHandler,
                    NoOpDaemonLogger.Instance);

                var responseTask = Task.Run(() =>
                    UnityIpcMethodHandlerTestInvoker.HandleAsync(
                        handler,
                        CreateRefreshRequest(start),
                        CancellationToken.None).AsTask());
                await TestAwaiter.WaitAsync(
                    finalObservationEntered.Task,
                    "refresh final result observation",
                    TimeSpan.FromSeconds(5));
                timeSource.Advance(TimeSpan.FromSeconds(1));
                await TestAwaiter.WaitAsync(
                    timeSource.DeadlineReached,
                    "refresh execution deadline",
                    TimeSpan.FromSeconds(5));
                releaseFinalObservation.TrySetResult(true);
                var response = await TestAwaiter.WaitAsync(
                    responseTask,
                    "refresh deadline terminal response",
                    TimeSpan.FromSeconds(5));

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    response.Errors[0].Code,
                    Is.EqualTo(LifecycleExecutionErrorCodes.DeadlineExceeded));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                    out IpcRefreshErrorResponse payload,
                    out _),
                    Is.True);
                Assert.That(
                    response.Payload.TryGetProperty("result", out _),
                    Is.False);
                Assert.That(
                    payload.LifecycleExecutionRef.Lifecycle,
                    Is.EqualTo(ExecutionLifecycle.Terminal));
                Assert.That(
                    payload.LifecycleExecutionRef.State.Value,
                    Is.EqualTo(TextVocabulary.GetText(
                        LifecycleExecutionState.Failed)));
                var terminal = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(
                    terminal.TerminalReason,
                    Is.EqualTo(
                        LifecycleExecutionTerminalReason.DeadlineExceeded));
                Assert.That(terminal.Result, Is.Not.Null);
                Assert.That(
                    terminal.TerminalGeneration,
                    Is.EqualTo(terminal.Result.Lifecycle.State.Generations));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenRefreshCompletes_PublishesTypedTerminalResult () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var refreshRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(CreateReadySnapshot()),
                    new RecordingUnityAssetRefreshController(
                        () => refreshRequestCount++));
                var request = CreateRefreshRequest(start);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    request,
                    CancellationToken.None);
                var checkpoint =
                    await new FileRefreshLifecycleExecutionCheckpointStore(
                            executionStore)
                        .ReadAsync(
                            start.LifecycleExecutionRef.Id,
                            CancellationToken.None);
                Assert.That(checkpoint.SideEffectAdmitted, Is.True);
                Assert.That(checkpoint.ProviderReturned, Is.True);
                WriteGuardedText(
                    executionStore.Paths.ResolveCheckpointPath(
                        LifecycleExecutionKind.Refresh,
                        start.LifecycleExecutionRef.Id,
                        "refresh-checkpoint.json"),
                    "{");
                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);
                var reconnectedResponse =
                    await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                        handler,
                        request,
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(
                    reconnectedResponse.Status,
                    Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcRefreshResponse payload,
                        out _),
                    Is.True);
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        reconnectedResponse.Payload,
                        out IpcRefreshResponse reconnectedPayload,
                        out _),
                    Is.True);
                Assert.That(
                    reconnectedPayload.LifecycleExecutionRef,
                    Is.EqualTo(payload.LifecycleExecutionRef));
                Assert.That(refreshRequestCount, Is.EqualTo(1));
                Assert.That(
                    payload.LifecycleExecutionRef.Lifecycle,
                    Is.EqualTo(ExecutionLifecycle.Terminal));
                Assert.That(
                    payload.Result.Refresh.DomainReloadGenerationBefore,
                    Is.EqualTo(2));
                Assert.That(
                    payload.Result.Refresh.DomainReloadGenerationAfter,
                    Is.EqualTo(2));
                Assert.That(payload.Result.ReadPostcondition, Is.Not.Null);
                Assert.That(
                    ReadTerminalRecord(
                        executionStore,
                        start.LifecycleExecutionRef.Id).TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.Completed));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenRefreshingAdmissionIsAlreadyOwned_RecoversWithoutRequestingRefresh () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var before = CreateReadySnapshot();
                var checkpointStore =
                    new FileRefreshLifecycleExecutionCheckpointStore(
                        executionStore);
                var prepared = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(before),
                    CancellationToken.None);
                var refreshingReference =
                    LifecycleExecutionReferenceFactory.CreateStateProjection(
                        start.LifecycleExecutionRef,
                        ExecutionLifecycle.Active,
                        LifecycleExecutionState.Refreshing);
                Assert.That(
                    (await executionStore.TryAcquireSideEffectRightAsync(
                        start.LifecycleExecutionRef,
                        refreshingReference,
                        start.Host
                            .CurrentEndpointRegistrationGenerationId,
                        CancellationToken.None)).Outcome,
                    Is.EqualTo(
                        LifecycleExecutionSideEffectRightOutcome.Acquired));
                var admitted = await checkpointStore.MarkAdmittedAsync(
                    prepared,
                    CancellationToken.None);
                _ = await checkpointStore.MarkDispatchPreparedAsync(
                    admitted,
                    new RefreshLifecycleDispatchCandidate(
                        DateTimeOffset.UtcNow,
                        before.State.Generations.DomainReloadGeneration),
                    CancellationToken.None);
                var provider =
                    new RecordingRefreshLifecycleExecutionProvider(
                        CreateReadySnapshot(
                            new UnityEditorGenerationSnapshot(1, 2, 4, 4)));
                var handler = new RefreshLifecycleExecutionHandler(
                    provider,
                    NoOpDaemonLogger.Instance,
                    executionStore,
                    checkpointStore,
                    new SystemLifecycleExecutionTimeSource());

                var outcome = await handler.ExecuteAsync(start);

                Assert.That(outcome.IsSuccess, Is.True);
                Assert.That(provider.RefreshRequestCount, Is.EqualTo(0));
                Assert.That(
                    outcome.LifecycleExecutionRef.Lifecycle,
                    Is.EqualTo(ExecutionLifecycle.Terminal));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenProviderReturnWasRecorded_CompletesWithoutReissuingProvider () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var before = CreateReadySnapshot();
                var checkpointStore =
                    new FileRefreshLifecycleExecutionCheckpointStore(
                        executionStore);
                var prepared = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(before),
                    CancellationToken.None);
                var refreshingReference =
                    LifecycleExecutionReferenceFactory.CreateStateProjection(
                        start.LifecycleExecutionRef,
                        ExecutionLifecycle.Active,
                        LifecycleExecutionState.Refreshing);
                Assert.That(
                    (await executionStore.TryAcquireSideEffectRightAsync(
                        start.LifecycleExecutionRef,
                        refreshingReference,
                        start.Host
                            .CurrentEndpointRegistrationGenerationId,
                        CancellationToken.None)).Outcome,
                    Is.EqualTo(
                        LifecycleExecutionSideEffectRightOutcome.Acquired));
                var admitted = await checkpointStore.MarkAdmittedAsync(
                    prepared,
                    CancellationToken.None);
                admitted = await checkpointStore.MarkDispatchPreparedAsync(
                    admitted,
                    new RefreshLifecycleDispatchCandidate(
                        DateTimeOffset.UtcNow,
                        before.State.Generations.DomainReloadGeneration),
                    CancellationToken.None);
                _ = await checkpointStore.MarkProviderReturnedAsync(
                    admitted,
                    CancellationToken.None);
                var refreshRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(before),
                    new RecordingUnityAssetRefreshController(
                        () => refreshRequestCount++));

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);

                Assert.That(refreshRequestCount, Is.EqualTo(0));
                Assert.That(
                    ReadTerminalRecord(
                        executionStore,
                        start.LifecycleExecutionRef.Id).TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.Completed));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenProviderReturnWasRecordedAtDeadline_PublishesAppliedEvidence () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    DateTimeOffset.UtcNow.AddMilliseconds(-100));
                var before = CreateReadySnapshot();
                var checkpointStore =
                    new FileRefreshLifecycleExecutionCheckpointStore(
                        executionStore);
                var prepared = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(before),
                    CancellationToken.None);
                var admitted = await checkpointStore.MarkAdmittedAsync(
                    prepared,
                    CancellationToken.None);
                var dispatchCandidate = new RefreshLifecycleDispatchCandidate(
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    before.State.Generations.DomainReloadGeneration);
                var dispatchPrepared =
                    await checkpointStore.MarkDispatchPreparedAsync(
                        admitted,
                        dispatchCandidate,
                        CancellationToken.None);
                _ = await checkpointStore.MarkProviderReturnedAsync(
                    dispatchPrepared,
                    CancellationToken.None);
                var refreshRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(before),
                    new RecordingUnityAssetRefreshController(
                        () => refreshRequestCount++));

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);
                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateRefreshRequest(start),
                    CancellationToken.None);

                Assert.That(refreshRequestCount, Is.EqualTo(0));
                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcRefreshErrorResponse payload,
                        out _),
                    Is.True);
                Assert.That(
                    payload.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.Applied));
                Assert.That(
                    payload.Refresh,
                    Is.EqualTo(new RefreshLifecycleStartEvidence(
                        dispatchCandidate.StartedAtUtc,
                        dispatchCandidate.DomainReloadGenerationBefore)));
                Assert.That(payload.ReadPostcondition, Is.Not.Null);
                var terminal = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(
                    terminal.TerminalReason,
                    Is.EqualTo(
                        LifecycleExecutionTerminalReason.DeadlineExceeded));
                Assert.That(
                    terminal.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.Applied));
                Assert.That(terminal.Result, Is.Null);
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenOnlyDispatchPreparationWasRecorded_DoesNotPublishStartEvidenceAtDeadline () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    DateTimeOffset.UtcNow.AddSeconds(1));
                var before = CreateReadySnapshot();
                var checkpointStore =
                    new FileRefreshLifecycleExecutionCheckpointStore(
                        executionStore);
                var prepared = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(before),
                    CancellationToken.None);
                var refreshingReference =
                    LifecycleExecutionReferenceFactory.CreateStateProjection(
                        start.LifecycleExecutionRef,
                        ExecutionLifecycle.Active,
                        LifecycleExecutionState.Refreshing);
                Assert.That(
                    (await executionStore.TryAcquireSideEffectRightAsync(
                        start.LifecycleExecutionRef,
                        refreshingReference,
                        start.Host
                            .CurrentEndpointRegistrationGenerationId,
                        CancellationToken.None)).Outcome,
                    Is.EqualTo(
                        LifecycleExecutionSideEffectRightOutcome.Acquired));
                var admitted = await checkpointStore.MarkAdmittedAsync(
                    prepared,
                    CancellationToken.None);
                _ = await checkpointStore.MarkDispatchPreparedAsync(
                    admitted,
                    new RefreshLifecycleDispatchCandidate(
                        DateTimeOffset.UtcNow,
                        before.State.Generations.DomainReloadGeneration),
                    CancellationToken.None);
                var refreshRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(before),
                    new RecordingUnityAssetRefreshController(
                        () => refreshRequestCount++));

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);
                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateRefreshRequest(start),
                    CancellationToken.None);

                Assert.That(refreshRequestCount, Is.EqualTo(0));
                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcRefreshErrorResponse payload,
                        out _),
                    Is.True);
                Assert.That(payload.Refresh, Is.Null);
                Assert.That(payload.ReadPostcondition, Is.Null);
                var terminal = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(
                    terminal.TerminalReason,
                    Is.EqualTo(
                        LifecycleExecutionTerminalReason.DeadlineExceeded));
                Assert.That(
                    terminal.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.Indeterminate));
                Assert.That(terminal.Result, Is.Null);
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenEndpointAdmissionIsRejected_DropsUnprovenGeneration () =>
            UniTask.ToCoroutine(async () =>
            {
                var rejections = new[]
                {
                    LifecycleExecutionTerminalReason.ProjectMismatch,
                    LifecycleExecutionTerminalReason.HostMismatch,
                    LifecycleExecutionTerminalReason.GenerationMismatch,
                    LifecycleExecutionTerminalReason.UnityExited,
                };
                foreach (var reason in rejections)
                {
                    using var scope = TemporaryStorageScope.Create();
                    var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                    var start = await RegisterAsync(executionStore);
                    var handler = CreateHandler(
                        executionStore,
                        new MutableUnityEditorReadinessGate(
                            CreateReadySnapshot()));

                    await handler.RecoverAsync(
                        new LifecycleExecutionRecoveryRequest(
                            start,
                            reason,
                            canAttributeCurrentProviderObservation: false),
                        CancellationToken.None);

                    var terminal = ReadTerminalRecord(
                        executionStore,
                        start.LifecycleExecutionRef.Id);
                    Assert.That(terminal.TerminalReason, Is.EqualTo(reason));
                    Assert.That(
                        terminal.ApplicationState,
                        Is.EqualTo(ExecutionApplicationState.NotApplied));
                    Assert.That(terminal.Result, Is.Null, reason.ToString());
                    Assert.That(
                        terminal.TerminalGeneration,
                        Is.Null,
                        reason.ToString());
                }
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenRefreshProviderRejects_PublishesActionFailedTerminal () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(CreateReadySnapshot()),
                    new RejectingUnityAssetRefreshController());
                var request = CreateRefreshRequest(start);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    request,
                    CancellationToken.None);
                var reconnectedResponse =
                    await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    request,
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    reconnectedResponse.Status,
                    Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcRefreshErrorResponse payload,
                        out _),
                    Is.True);
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        reconnectedResponse.Payload,
                        out IpcRefreshErrorResponse reconnectedPayload,
                        out _),
                    Is.True);
                Assert.That(
                    payload.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.Indeterminate));
                Assert.That(payload.Refresh, Is.Not.Null);
                Assert.That(
                    reconnectedPayload.Refresh,
                    Is.EqualTo(payload.Refresh));
                var terminal = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(
                    terminal.TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.ActionFailed));
                Assert.That(
                    terminal.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.Indeterminate));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenRefreshAdapterHasInternalFailure_PropagatesWithoutTerminalizing () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(CreateReadySnapshot()),
                    new RecordingUnityAssetRefreshController(
                        () => throw new InvalidOperationException("adapter bug")));

                InvalidOperationException observedException = null;
                try
                {
                    await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                        handler,
                        CreateRefreshRequest(start),
                        CancellationToken.None);
                }
                catch (InvalidOperationException exception)
                {
                    observedException = exception;
                }
                Assert.That(observedException, Is.Not.Null);

                var execution = await executionStore.ReadAsync(
                    LifecycleExecutionKind.Refresh,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                Assert.That(execution, Is.Not.Null);
                Assert.That(execution.IsTerminal, Is.False);
            });

        private static async ValueTask<LifecycleExecutionStartBinding> RegisterAsync (
            FileLifecycleExecutionStore executionStore,
            DateTimeOffset? deadlineUtc = null,
            DateTimeOffset? startedAtUtc = null)
        {
            var definition =
                new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
            var result = await executionStore.StartAsync(
                definition,
                Guid.NewGuid(),
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                ProjectIdentity,
                Host,
                CreateReadySnapshot().State.Generations,
                deadlineUtc ?? DateTimeOffset.UtcNow.AddSeconds(10),
                startedAtUtc ?? DateTimeOffset.UtcNow.AddSeconds(-1),
                CancellationToken.None);
            return result.Binding
                ?? throw new AssertionException(
                    $"Refresh registration failed: {result.Outcome}");
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenOnlyStartWasRegisteredBeforeDeadline_RemainsOpenWithoutRefresh () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var refreshRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(CreateReadySnapshot()),
                    new RecordingUnityAssetRefreshController(
                        () => refreshRequestCount++));

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);

                Assert.That(refreshRequestCount, Is.EqualTo(0));
                var execution = await executionStore.ReadAsync(
                    LifecycleExecutionKind.Refresh,
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
        public IEnumerator RecoverAsync_WhenOnlyStartWasRegisteredAfterDeadline_PublishesNotAppliedTerminalWithoutRefresh () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    DateTimeOffset.UtcNow.AddMilliseconds(-100));
                var refreshRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(CreateReadySnapshot()),
                    new RecordingUnityAssetRefreshController(
                        () => refreshRequestCount++));

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);

                Assert.That(refreshRequestCount, Is.EqualTo(0));
                Assert.That(
                    await new FileRefreshLifecycleExecutionCheckpointStore(
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

        private static RefreshHandlerFixture CreateHandler (
            FileLifecycleExecutionStore executionStore,
            MutableUnityEditorReadinessGate readinessGate,
            IUnityAssetRefreshController assetRefreshController = null,
            IDaemonLogger daemonLogger = null)
        {
            daemonLogger ??= NoOpDaemonLogger.Instance;
            var provider = new UnityEditorRefreshLifecycleExecutionProvider(
                readinessGate,
                ProjectIdentity,
                new StubServerVersionProvider("1.2.3"),
                new ImmediateUnityMutationLaneControl(),
                assetRefreshController ?? new UnityAssetRefreshController(),
                new UnityEditorUpdateAwaiterAdapter());
            var executionHandler = new RefreshLifecycleExecutionHandler(
                provider,
                daemonLogger,
                executionStore,
                new FileRefreshLifecycleExecutionCheckpointStore(
                    executionStore),
                new SystemLifecycleExecutionTimeSource());
            return new RefreshHandlerFixture(
                new RefreshUnityIpcMethodHandler(
                    executionHandler,
                    daemonLogger),
                executionHandler);
        }

        private sealed class RefreshHandlerFixture :
            IUnityIpcMethodHandler
        {
            private readonly RefreshUnityIpcMethodHandler ipcHandler;
            private readonly RefreshLifecycleExecutionHandler executionHandler;

            public RefreshHandlerFixture (
                RefreshUnityIpcMethodHandler ipcHandler,
                RefreshLifecycleExecutionHandler executionHandler)
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

        private sealed class RecordingRefreshLifecycleExecutionProvider :
            IRefreshLifecycleExecutionProvider
        {
            private readonly UnityEditorRuntimeObservation observation;
            private readonly TaskCompletionSource<bool> finalObservationEntered;
            private readonly Task releaseFinalObservation;
            private int lifecycleObservationCount;

            public RecordingRefreshLifecycleExecutionProvider (
                UnityEditorRuntimeObservation observation)
            {
                this.observation = observation;
            }

            public RecordingRefreshLifecycleExecutionProvider (
                UnityEditorRuntimeObservation observation,
                TaskCompletionSource<bool> finalObservationEntered,
                Task releaseFinalObservation)
                : this(observation)
            {
                this.finalObservationEntered = finalObservationEntered
                    ?? throw new ArgumentNullException(
                        nameof(finalObservationEntered));
                this.releaseFinalObservation = releaseFinalObservation
                    ?? throw new ArgumentNullException(
                        nameof(releaseFinalObservation));
            }

            public UnityProjectIdentity Project => ProjectIdentity;

            public int RefreshRequestCount { get; private set; }

            public int MutationCount { get; private set; }

            public bool MutationCompleted { get; private set; }

            public UnityEditorRuntimeObservation CaptureObservation ()
            {
                return observation;
            }

            public UnityEditorObservation CreateLifecycleObservation (
                UnityEditorRuntimeObservation currentObservation)
            {
                lifecycleObservationCount++;
                if (lifecycleObservationCount == 2
                    && finalObservationEntered != null)
                {
                    finalObservationEntered.TrySetResult(true);
                    releaseFinalObservation.GetAwaiter().GetResult();
                }

                return UnityLifecycleResponseFactory.Create(
                    ProjectIdentity,
                    "1.2.3",
                    currentObservation);
            }

            public IUnityMutationActivity BeginMutation ()
            {
                MutationCount++;
                return new RecordingMutationActivity(
                    () => MutationCompleted = true);
            }

            public void RequestRefresh ()
            {
                RefreshRequestCount++;
            }

            public Task WaitForNextUpdateAsync (
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            private sealed class RecordingMutationActivity :
                IUnityMutationActivity
            {
                private readonly Action complete;

                public RecordingMutationActivity (Action complete)
                {
                    this.complete = complete;
                }

                public void Complete ()
                {
                    complete();
                }
            }
        }

        private sealed class RejectingUnityAssetRefreshController :
            IUnityAssetRefreshController
        {
            public void Refresh ()
            {
                throw new UnityAssetRefreshException(
                    "Unity rejected the asset refresh request.",
                    new InvalidOperationException("provider failure"));
            }
        }

        private static IpcRequestEnvelope CreateRefreshRequest (
            LifecycleExecutionStartBinding start)
        {
            return new IpcRequestEnvelope(
                IpcProtocol.CurrentVersion,
                Guid.NewGuid(),
                "session-token",
                TextVocabulary.GetText(UnityIpcMethod.Refresh),
                IpcPayloadCodec.SerializeToElement(new IpcRefreshRequest(start)),
                "single",
                DateTimeOffset.UtcNow.AddSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
        }

        private static RefreshLifecycleExecutionTerminalRecord ReadTerminalRecord (
            FileLifecycleExecutionStore executionStore,
            Guid executionId)
        {
            return LifecycleExecutionHandlerTestSupport.ReadTerminalRecord
                <RefreshLifecycleExecutionTerminalRecord>(
                executionStore,
                LifecycleExecutionKind.Refresh,
                executionId);
        }

        private static UnityEditorObservation CreateObservation (
            UnityEditorRuntimeObservation observation)
        {
            return UnityLifecycleResponseFactory.Create(
                ProjectIdentity,
                "1.2.3",
                observation);
        }

        private static UnityEditorRuntimeObservation CreateReadySnapshot (
            UnityEditorGenerationSnapshot generations = null)
        {
            return new UnityEditorRuntimeObservation(
                new UnityEditorStateSnapshot(
                    UnityEditorMode.Batchmode,
                    UnityEditorLifecycleState.Ready,
                    UnityEditorCompileState.Ready,
                    generations ?? new UnityEditorGenerationSnapshot(1, 2, 3, 4),
                    new UnityEditorPlayModeSnapshot(
                        UnityEditorPlayModeState.Stopped,
                        UnityEditorPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                DateTimeOffset.UtcNow);
        }

    }
}
