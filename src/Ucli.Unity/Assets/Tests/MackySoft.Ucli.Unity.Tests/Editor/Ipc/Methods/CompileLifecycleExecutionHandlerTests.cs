using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine.TestTools;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;
using static MackySoft.Ucli.Unity.Tests.LifecycleExecutionHandlerTestSupport;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class CompileLifecycleExecutionHandlerTests
    {
        private const string ServerVersion = "1.2.3";

        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("compile-lifecycle-execution");

        private static readonly UnityProjectIdentity ProjectIdentity = new(
            ProjectPathTestValues.RepositoryUnityProject,
            ProjectFingerprint,
            "2023.2.22f1");

        private static readonly LifecycleExecutionHostRegistration Host = new(
            new ProcessIdentity(42, 123),
            Guid.Parse("39182a43-045d-4ca5-9c04-ab8d88a7961f"),
            Guid.Parse("93556164-9684-42f6-8ba3-01cf5568671e"),
            Guid.Parse("93556164-9684-42f6-8ba3-01cf5568671e"));

        [Test]
        [Category("Size.Small")]
        public async Task CheckpointStore_WhenSeparateInstancesPrepareSameExecutionConcurrently_PreservesOnePreparation ()
        {
            const int ConcurrentWriterCount = 8;
            using var scope = TemporaryStorageScope.Create();
            var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
            var start = await RegisterAsync(executionStore);
            var startGate = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var writes =
                new Task<CompileLifecycleExecutionCheckpoint>[
                    ConcurrentWriterCount
                ];
            for (var index = 0; index < writes.Length; index++)
            {
                var generation = index + 1;
                var before = CreateReadySnapshot(
                    generations:
                        new UnityEditorGenerationSnapshot(
                            generation,
                            generation,
                            generation,
                            generation));
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                writes[index] = Task.Run(async () =>
                {
                    _ = await startGate.Task;
                    return await checkpointStore.WritePreparedAsync(
                        start.LifecycleExecutionRef.Id,
                        CreateObservation(before),
                        CreatePendingResult(before),
                        CancellationToken.None);
                });
            }

            startGate.SetResult(true);
            var completedWrites = await Task.WhenAll(writes);
            var persisted =
                await new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore)
                    .ReadAsync(
                        start.LifecycleExecutionRef.Id,
                        CancellationToken.None);

            Assert.That(persisted, Is.Not.Null);
            for (var index = 0; index < completedWrites.Length; index++)
            {
                Assert.That(
                    completedWrites[index]
                        .Before.State.Generations.CompileGeneration,
                    Is.EqualTo(
                        persisted.Before.State.Generations.CompileGeneration));
            }
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenRefreshRequiresNoCompilation_RemainsRefreshingAndCompletesWithoutCompilation () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var provider = new ControlledCompileLifecycleExecutionProvider(
                    CreateReadySnapshot());
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                var handler = new CompileLifecycleExecutionHandler(
                    provider,
                    NoOpDaemonLogger.Instance,
                    executionStore,
                    checkpointStore);

                var executionTask = handler.ExecuteAsync(start).AsTask();
                await TestAwaiter.WaitUntilAsync(
                    () => provider.RefreshRequestCount == 1,
                    "compile refresh dispatch",
                    TimeSpan.FromSeconds(5));
                _ = await TestAwaiter.WaitAsync(
                    WaitForProviderReturnAsync(
                        checkpointStore,
                        start.LifecycleExecutionRef.Id),
                    "compile refresh provider return",
                    TimeSpan.FromSeconds(5));
                provider.Advance(CreateReimportingSnapshot());
                await TestAwaiter.WaitUntilAsync(
                    () => provider.UpdateWaitCount >= 2,
                    "post-refresh observation",
                    TimeSpan.FromSeconds(5));
                var refreshing = await executionStore.ReadAsync(
                    LifecycleExecutionKind.Compile,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);

                Assert.That(refreshing, Is.Not.Null);
                Assert.That(
                    refreshing.CurrentReference.State.Value,
                    Is.EqualTo(TextVocabulary.GetText(
                        LifecycleExecutionState.Refreshing)));
                Assert.That(
                    refreshing.CurrentReference.Lifecycle,
                    Is.EqualTo(ExecutionLifecycle.Active));
                provider.Advance(CreateReadySnapshot());
                await TestAwaiter.WaitUntilAsync(
                    () => provider.UpdateWaitCount >= 3,
                    "first stable no-compilation observation",
                    TimeSpan.FromSeconds(5));
                provider.Advance(CreateReadySnapshot());
                var outcome = await TestAwaiter.WaitAsync(
                    executionTask,
                    "no-compilation lifecycle completion",
                    TimeSpan.FromSeconds(5));

                Assert.That(outcome.IsSuccess, Is.True);
                Assert.That(outcome.Result.ScriptCompilation.Started, Is.False);
                Assert.That(outcome.Result.ScriptCompilation.Completed, Is.True);
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenCompilationIsObserved_PersistsCompilingBeforeCompletingWithCompilation () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var provider = new ControlledCompileLifecycleExecutionProvider(
                    CreateReadySnapshot());
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                var handler = new CompileLifecycleExecutionHandler(
                    provider,
                    NoOpDaemonLogger.Instance,
                    executionStore,
                    checkpointStore);

                var executionTask = handler.ExecuteAsync(start).AsTask();
                await TestAwaiter.WaitUntilAsync(
                    () => provider.RefreshRequestCount == 1,
                    "compile refresh dispatch",
                    TimeSpan.FromSeconds(5));
                _ = await TestAwaiter.WaitAsync(
                    WaitForProviderReturnAsync(
                        checkpointStore,
                        start.LifecycleExecutionRef.Id),
                    "compile refresh provider return",
                    TimeSpan.FromSeconds(5));
                provider.StartCompilation(batchId: 2);
                provider.Advance(CreateCompilingSnapshot());
                var compiling = await TestAwaiter.WaitAsync(
                    WaitForExecutionStateAsync(
                        executionStore,
                        start.LifecycleExecutionRef.Id,
                        LifecycleExecutionState.Compiling),
                    "durable compiling state",
                    TimeSpan.FromSeconds(5));

                Assert.That(
                    compiling.CurrentReference.Lifecycle,
                    Is.EqualTo(ExecutionLifecycle.Active));
                provider.CompleteCompilation(batchId: 2);
                provider.Advance(CreateReadySnapshot(
                    generations:
                        new UnityEditorGenerationSnapshot(2, 2, 4, 4)));
                await TestAwaiter.WaitUntilAsync(
                    () => provider.UpdateWaitCount >= 3,
                    "second stable compile observation",
                    TimeSpan.FromSeconds(5));
                provider.Advance(CreateReadySnapshot(
                    generations:
                        new UnityEditorGenerationSnapshot(2, 2, 4, 4)));
                var outcome = await TestAwaiter.WaitAsync(
                    executionTask,
                    "compile lifecycle completion",
                    TimeSpan.FromSeconds(5));

                Assert.That(outcome.IsSuccess, Is.True);
                Assert.That(outcome.Result.ScriptCompilation.Started, Is.True);
                Assert.That(outcome.Result.ScriptCompilation.Completed, Is.True);
                Assert.That(
                    outcome.Result.ScriptCompilation.CompileGenerationAfter,
                    Is.EqualTo(2));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenSameExecutionAlreadyEnteredRefreshing_RecoversPreparedCheckpointWithoutRedispatch () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                var before = CreateReadySnapshot();
                var prepared = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(before),
                    CreatePendingResult(before),
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
                _ = await checkpointStore.MarkAdmittedAsync(
                    prepared,
                    CancellationToken.None);
                var after = CreateReadySnapshot(
                    generations:
                        new UnityEditorGenerationSnapshot(1, 2, 4, 4));
                var provider =
                    new RecordingCompileLifecycleExecutionProvider(after);
                var handler = new CompileLifecycleExecutionHandler(
                    provider,
                    NoOpDaemonLogger.Instance,
                    executionStore,
                    checkpointStore);

                var outcome = await handler.ExecuteAsync(start);

                Assert.That(outcome.IsSuccess, Is.True);
                Assert.That(provider.RefreshRequestCount, Is.EqualTo(0));
                Assert.That(provider.MutationCount, Is.EqualTo(0));
                var checkpoint = await checkpointStore.ReadAsync(
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                Assert.That(
                    checkpoint.Before,
                    Is.EqualTo(CreateObservation(before)));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenProviderFixesResultAfterDeadline_PublishesFailedTerminalWithResult () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    DateTimeOffset.UtcNow.AddSeconds(1));
                var provider =
                    new RecordingCompileLifecycleExecutionProvider(
                        CreateReadySnapshot(),
                        start.DeadlineUtc.AddMilliseconds(20));
                var executionHandler = new CompileLifecycleExecutionHandler(
                    provider,
                    NoOpDaemonLogger.Instance,
                    executionStore,
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore));
                var handler = new CompileUnityIpcMethodHandler(
                    executionHandler,
                    NoOpDaemonLogger.Instance);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateCompileRequest(start),
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    response.Errors[0].Code,
                    Is.EqualTo(LifecycleExecutionErrorCodes.DeadlineExceeded));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcCompileErrorResponse payload,
                        out _),
                    Is.True);
                Assert.That(payload.Result, Is.Not.Null);
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
                Assert.That(terminal.Result, Is.EqualTo(payload.Result));
                Assert.That(
                    terminal.TerminalGeneration,
                    Is.EqualTo(terminal.Result.Lifecycle.State.Generations));
                Assert.That(terminal.Verdict, Is.Null);
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenProviderReturnedWithUnsettledDiagnosticsAtDeadline_PreservesConfirmedPartialResult () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    DateTimeOffset.UtcNow.AddMilliseconds(-100));
                var before = CreateReadySnapshot();
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                var prepared = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(before),
                    CreatePendingResult(before),
                    CancellationToken.None);
                var admitted = await checkpointStore.MarkAdmittedAsync(
                    prepared,
                    CancellationToken.None);
                var providerReturned =
                    await checkpointStore.MarkProviderReturnedAsync(
                    admitted,
                    CancellationToken.None);
                var primaryDiagnostic = new UnityEditorPrimaryDiagnostic(
                    UnityEditorPrimaryDiagnosticKind.Compiler,
                    "CS1002",
                    "Assets/Partial.cs",
                    9,
                    3,
                    "Semicolon expected.");
                var diagnostics = checkpointStore.CreateDiagnosticsSink(
                    start.LifecycleExecutionRef.Id);
                diagnostics.StartBatch(batchId: 7);
                diagnostics.RecordAssembly(
                    batchId: 7,
                    assemblyIdentity: "Partial.dll",
                    errorCount: 1,
                    warningCount: 2,
                    primaryDiagnostic);
                var terminalObservation = new UnityEditorRuntimeObservation(
                    new UnityEditorStateSnapshot(
                        UnityEditorMode.Batchmode,
                        UnityEditorLifecycleState.Compiling,
                        UnityEditorCompileState.Compiling,
                        new UnityEditorGenerationSnapshot(2, 3, 4, 5),
                        new UnityEditorPlayModeSnapshot(
                            UnityEditorPlayModeState.Stopped,
                            UnityEditorPlayModeTransition.None,
                            IsPlaying: false,
                            IsPlayingOrWillChangePlaymode: false)),
                    DateTimeOffset.UtcNow,
                    primaryDiagnostic: null);
                var handler = new CompileLifecycleExecutionHandler(
                    new RecordingCompileLifecycleExecutionProvider(
                        terminalObservation),
                    NoOpDaemonLogger.Instance,
                    executionStore,
                    checkpointStore);

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);

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
                Assert.That(terminal.Result.Refresh.Completed, Is.True);
                Assert.That(
                    terminal.Result.Refresh.CompletedAtUtc,
                    Is.EqualTo(providerReturned.ProviderReturnedAtUtc));
                Assert.That(
                    terminal.Result.ScriptCompilation.Started,
                    Is.True);
                Assert.That(
                    terminal.Result.ScriptCompilation.Completed,
                    Is.False);
                Assert.That(
                    terminal.Result.ScriptCompilation.CompileGenerationAfter,
                    Is.EqualTo(2));
                Assert.That(
                    terminal.Result.ScriptCompilation.Diagnostics.ErrorCount,
                    Is.EqualTo(1));
                Assert.That(
                    terminal.Result.ScriptCompilation.Diagnostics.WarningCount,
                    Is.EqualTo(2));
                Assert.That(
                    terminal.Result.ScriptCompilation.Diagnostics
                        .PrimaryDiagnostic,
                    Is.EqualTo(primaryDiagnostic));
                Assert.That(
                    terminal.Result.DomainReload.ReloadRequired,
                    Is.True);
                Assert.That(
                    terminal.Result.DomainReload.ReloadObserved,
                    Is.True);
                Assert.That(
                    terminal.Result.DomainReload.Settled,
                    Is.False);
                Assert.That(
                    terminal.Result.Lifecycle.State.LifecycleState,
                    Is.EqualTo(UnityEditorLifecycleState.Compiling));
                Assert.That(
                    terminal.TerminalGeneration,
                    Is.EqualTo(terminalObservation.State.Generations));
                Assert.That(terminal.Verdict, Is.Null);
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenDeadlineWinsRejectedAdmission_DoesNotAttributeCurrentProviderObservation () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    DateTimeOffset.UtcNow.AddMilliseconds(-100));
                var before = CreateReadySnapshot();
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                var prepared = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(before),
                    CreatePendingResult(before),
                    CancellationToken.None);
                var admitted = await checkpointStore.MarkAdmittedAsync(
                    prepared,
                    CancellationToken.None);
                _ = await checkpointStore.MarkProviderReturnedAsync(
                    admitted,
                    CancellationToken.None);
                var provider =
                    new RecordingCompileLifecycleExecutionProvider(
                        CreateReadySnapshot());
                var handler = new CompileLifecycleExecutionHandler(
                    provider,
                    NoOpDaemonLogger.Instance,
                    executionStore,
                    checkpointStore);

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        start,
                        LifecycleExecutionTerminalReason.DeadlineExceeded,
                        canAttributeCurrentProviderObservation: false),
                    CancellationToken.None);

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
                Assert.That(terminal.TerminalGeneration, Is.Null);
                Assert.That(provider.ObservationCount, Is.EqualTo(0));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenCompileCompletesWithDiagnostic_PublishesCompletedTerminalWithExplicitFailVerdict () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var diagnostic = new UnityEditorPrimaryDiagnostic(
                    UnityEditorPrimaryDiagnosticKind.Compiler,
                    "CS0001",
                    "Assets/Example.cs",
                    1,
                    1,
                    "Confirmed compiler diagnostic.");
                var refreshRequestCount = 0;
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(
                        CreateReadySnapshot(diagnostic)),
                    assetRefreshController:
                        new RecordingUnityAssetRefreshController(
                            () => refreshRequestCount++));
                var request = CreateCompileRequest(start);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    request,
                    CancellationToken.None);
                var checkpoint =
                    await new FileCompileLifecycleExecutionCheckpointStore(
                            executionStore)
                        .ReadAsync(
                            start.LifecycleExecutionRef.Id,
                            CancellationToken.None);
                Assert.That(
                    checkpoint.ProviderReturnedAtUtc,
                    Is.Not.Null);
                File.WriteAllText(
                    executionStore.Paths.ResolveCheckpointPath(
                        LifecycleExecutionKind.Compile,
                        start.LifecycleExecutionRef.Id,
                        "compile-checkpoint.json").Value,
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
                        out IpcCompileResponse payload,
                        out _),
                    Is.True);
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        reconnectedResponse.Payload,
                        out IpcCompileResponse reconnectedPayload,
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
                    payload.Result.ScriptCompilation.Diagnostics.ErrorCount,
                    Is.EqualTo(1));
                var terminal = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(
                    terminal.TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.Completed));
                Assert.That(terminal.Result, Is.EqualTo(payload.Result));
                Assert.That(terminal.Verdict, Is.EqualTo(Verdict.Fail));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenRefreshProviderRejects_PublishesActionFailedTerminal () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var readinessGate = new MutableUnityEditorReadinessGate(
                    CreateReadySnapshot());
                var laterSnapshot = CreateReadySnapshot(
                    generations:
                        new UnityEditorGenerationSnapshot(1, 2, 3, 5));
                var handler = CreateHandler(
                    executionStore,
                    readinessGate,
                    assetRefreshController:
                        new RejectingUnityAssetRefreshController(
                            () => readinessGate.Snapshot = laterSnapshot));

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateCompileRequest(start),
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcCompileErrorResponse payload,
                        out _),
                    Is.True);
                Assert.That(
                    payload.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.Indeterminate));
                Assert.That(payload.Result, Is.Not.Null);
                var terminal = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(
                    terminal.TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.ActionFailed));
                Assert.That(terminal.Result, Is.Not.Null);
                Assert.That(
                    terminal.TerminalGeneration,
                    Is.EqualTo(terminal.Result.Lifecycle.State.Generations));
                Assert.That(terminal.Verdict, Is.Null);
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenProviderReturnWasRecorded_ObservesCompletionWithoutReissuingProvider () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var before = CreateReadySnapshot();
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(executionStore);
                var prepared = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(before),
                    CreatePendingResult(before),
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
                _ = await checkpointStore.MarkProviderReturnedAsync(
                    admitted,
                    CancellationToken.None);
                var mutationLane = new ImmediateUnityMutationLaneControl();
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(before),
                    checkpointStore,
                    mutationLane);
                var current = await executionStore.ReadAsync(
                    LifecycleExecutionKind.Compile,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        current.Start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);

                Assert.That(mutationLane.BeginCount, Is.EqualTo(0));
                var execution = await executionStore.ReadAsync(
                    LifecycleExecutionKind.Compile,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                Assert.That(execution.IsTerminal, Is.True);
                Assert.That(
                    ReadTerminalRecord(
                        executionStore,
                        start.LifecycleExecutionRef.Id).TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.Completed));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenCompilationFinishedBeforeDomainReload_PreservesDiagnosticsWithoutReissuingProvider () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var before = CreateReadySnapshot();
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                var prepared = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(before),
                    CreatePendingResult(before),
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
                var diagnostic = new UnityEditorPrimaryDiagnostic(
                    UnityEditorPrimaryDiagnosticKind.Compiler,
                    "CS1002",
                    "Assets/Reloaded.cs",
                    12,
                    4,
                    "Semicolon expected.");
                var diagnostics =
                    checkpointStore.CreateDiagnosticsSink(
                        start.LifecycleExecutionRef.Id);
                const long batchId = 3;
                diagnostics.StartBatch(batchId);
                diagnostics.RecordAssembly(
                    batchId,
                    "Library/ScriptAssemblies/Assembly-CSharp.dll",
                    errorCount: 1,
                    warningCount: 2,
                    diagnostic);
                diagnostics.CompleteBatch(batchId);

                var reloadedCheckpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                var reloadedDiagnostics =
                    reloadedCheckpointStore.CreateDiagnosticsSink(
                        start.LifecycleExecutionRef.Id);
                Assert.That(
                    reloadedDiagnostics.LastProcessedBatchId,
                    Is.EqualTo(batchId));
                reloadedDiagnostics.RecordAssembly(
                    batchId,
                    "Library/ScriptAssemblies/Assembly-CSharp.dll",
                    errorCount: 1,
                    warningCount: 2,
                    diagnostic);
                Assert.Throws<IOException>(() =>
                    reloadedDiagnostics.RecordAssembly(
                        batchId,
                        "Library/ScriptAssemblies/Assembly-CSharp.dll",
                        errorCount: 1,
                        warningCount: 3,
                        diagnostic));
                reloadedDiagnostics.CompleteBatch(batchId);
                _ = await reloadedCheckpointStore.MarkProviderReturnedAsync(
                    admitted,
                    CancellationToken.None);
                var after = CreateReadySnapshot(
                    generations:
                        new UnityEditorGenerationSnapshot(1, 3, 4, 5));
                var provider =
                    new RecordingCompileLifecycleExecutionProvider(after);
                var handler = new CompileLifecycleExecutionHandler(
                    provider,
                    NoOpDaemonLogger.Instance,
                    executionStore,
                    reloadedCheckpointStore);
                var current = await executionStore.ReadAsync(
                    LifecycleExecutionKind.Compile,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);

                await handler.RecoverAsync(
                    new LifecycleExecutionRecoveryRequest(
                        current.Start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation: true),
                    CancellationToken.None);

                Assert.That(provider.RefreshRequestCount, Is.EqualTo(0));
                Assert.That(provider.MutationCount, Is.EqualTo(0));
                var terminal = ReadTerminalRecord(
                    executionStore,
                    start.LifecycleExecutionRef.Id);
                Assert.That(
                    terminal.TerminalReason,
                    Is.EqualTo(LifecycleExecutionTerminalReason.Completed));
                Assert.That(
                    terminal.Result.ScriptCompilation.Started,
                    Is.True);
                Assert.That(
                    terminal.Result.ScriptCompilation.Completed,
                    Is.True);
                Assert.That(
                    terminal.Result.ScriptCompilation.Diagnostics.ErrorCount,
                    Is.EqualTo(1));
                Assert.That(
                    terminal.Result.ScriptCompilation.Diagnostics.WarningCount,
                    Is.EqualTo(2));
                Assert.That(
                    terminal.Result.ScriptCompilation.Diagnostics.PrimaryDiagnostic,
                    Is.EqualTo(diagnostic));
                var persisted = await reloadedCheckpointStore.ReadAsync(
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                Assert.That(
                    persisted.ProviderReturnedAtUtc,
                    Is.Not.Null);
                Assert.That(persisted.Diagnostics.Completed, Is.True);
                Assert.That(persisted.Diagnostics.ErrorCount, Is.EqualTo(1));
                Assert.That(
                    persisted.Diagnostics.ProcessedBatchIds,
                    Is.EqualTo(new[] { batchId }));
                Assert.That(
                    persisted.Diagnostics.ProcessedAssemblies.Count,
                    Is.EqualTo(1));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DiagnosticsObservation_WhenReloadMissesCompilationStarted_StartsNextDurableBatch () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                _ = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(CreateReadySnapshot()),
                    CreatePendingResult(CreateReadySnapshot()),
                    CancellationToken.None);
                var firstSink = checkpointStore.CreateDiagnosticsSink(
                    start.LifecycleExecutionRef.Id);
                const long completedBatchId = 3;
                const string assemblyIdentity =
                    "Library/ScriptAssemblies/Assembly-CSharp.dll";
                firstSink.StartBatch(completedBatchId);
                firstSink.RecordAssembly(
                    completedBatchId,
                    assemblyIdentity,
                    errorCount: 1,
                    warningCount: 0,
                    primaryDiagnostic: null);
                firstSink.CompleteBatch(completedBatchId);

                var reloadedStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                var reloadedSink = reloadedStore.CreateDiagnosticsSink(
                    start.LifecycleExecutionRef.Id);
                using var observation =
                    new UnityEditorCompileLifecycleExecutionProvider
                        .DiagnosticsObservation(
                            reloadedSink,
                            () => completedBatchId);
                var noMessages = Array.Empty<CompilerMessage>();
                observation.OnAssemblyCompilationFinished(
                    assemblyIdentity,
                    noMessages);
                observation.OnAssemblyCompilationFinished(
                    assemblyIdentity,
                    noMessages);
                var compilationContext = new object();
                observation.OnCompilationFinished(compilationContext);
                observation.OnCompilationFinished(compilationContext);

                var checkpoint = await reloadedStore.ReadAsync(
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                Assert.That(
                    checkpoint.Diagnostics.ProcessedBatchIds,
                    Is.EqualTo(new[]
                    {
                        completedBatchId,
                        completedBatchId + 1,
                    }));
                Assert.That(
                    checkpoint.Diagnostics.ProcessedAssemblies.Count,
                    Is.EqualTo(2));
                Assert.That(
                    checkpoint.Diagnostics.ProcessedAssemblies[0]
                        .AssemblyIdentity,
                    Is.EqualTo(assemblyIdentity));
                Assert.That(
                    checkpoint.Diagnostics.ProcessedAssemblies[1]
                        .AssemblyIdentity,
                    Is.EqualTo(assemblyIdentity));
                Assert.That(
                    checkpoint.Diagnostics.ProcessedAssemblies[0].BatchId,
                    Is.EqualTo(completedBatchId));
                Assert.That(
                    checkpoint.Diagnostics.ProcessedAssemblies[1].BatchId,
                    Is.EqualTo(completedBatchId + 1));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DiagnosticsObservation_WhenReloadReceivesOnlyCompilationFinished_CompletesNextDurableBatchOnce () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                _ = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(CreateReadySnapshot()),
                    CreatePendingResult(CreateReadySnapshot()),
                    CancellationToken.None);
                var firstSink = checkpointStore.CreateDiagnosticsSink(
                    start.LifecycleExecutionRef.Id);
                const long completedBatchId = 7;
                firstSink.StartBatch(completedBatchId);
                firstSink.CompleteBatch(completedBatchId);

                var reloadedStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                using var observation =
                    new UnityEditorCompileLifecycleExecutionProvider
                        .DiagnosticsObservation(
                            reloadedStore.CreateDiagnosticsSink(
                                start.LifecycleExecutionRef.Id),
                            () => completedBatchId);
                var compilationContext = new object();
                observation.OnCompilationFinished(compilationContext);
                observation.OnCompilationFinished(compilationContext);

                var checkpoint = await reloadedStore.ReadAsync(
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                Assert.That(
                    checkpoint.Diagnostics.ProcessedBatchIds,
                    Is.EqualTo(new[]
                    {
                        completedBatchId,
                        completedBatchId + 1,
                    }));
                Assert.That(
                    checkpoint.Diagnostics.ActiveBatchId,
                    Is.Null);
                Assert.That(checkpoint.Diagnostics.Completed, Is.True);
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenCompileGenerationAdvancedWithoutFinishedDiagnostics_DoesNotPublishCompletedTerminal () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(executionStore);
                var before = CreateReadySnapshot();
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                var prepared = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(before),
                    CreatePendingResult(before),
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
                _ = await checkpointStore.MarkProviderReturnedAsync(
                    admitted,
                    CancellationToken.None);
                checkpointStore.CreateDiagnosticsSink(
                        start.LifecycleExecutionRef.Id)
                    .StartBatch(batchId: 3);
                var after = CreateReadySnapshot(
                    generations:
                        new UnityEditorGenerationSnapshot(1, 3, 3, 4));
                var handler = new CompileLifecycleExecutionHandler(
                    new RecordingCompileLifecycleExecutionProvider(after),
                    NoOpDaemonLogger.Instance,
                    executionStore,
                    checkpointStore);

                IOException observedException = null;
                try
                {
                    await handler.RecoverAsync(
                        new LifecycleExecutionRecoveryRequest(
                            start,
                            rejectionReason: null,
                            canAttributeCurrentProviderObservation: true),
                        CancellationToken.None);
                }
                catch (IOException exception)
                {
                    observedException = exception;
                }

                Assert.That(observedException, Is.Not.Null);

                var execution = await executionStore.ReadAsync(
                    LifecycleExecutionKind.Compile,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                Assert.That(execution.IsTerminal, Is.False);
                Assert.That(
                    File.Exists(
                        executionStore.Paths.ResolveTerminalRecordPath(
                            LifecycleExecutionKind.Compile,
                            start.LifecycleExecutionRef.Id).Target.Value),
                    Is.False);
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RecoverAsync_WhenOnlyDispatchPreparationWasRecorded_DoesNotPublishPartialResultAtDeadline () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
                var start = await RegisterAsync(
                    executionStore,
                    DateTimeOffset.UtcNow.AddSeconds(1));
                var before = CreateReadySnapshot();
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                var prepared = await checkpointStore.WritePreparedAsync(
                    start.LifecycleExecutionRef.Id,
                    CreateObservation(before),
                    CreatePendingResult(before, requested: false),
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
                    DateTimeOffset.UtcNow,
                    CancellationToken.None);
                var refreshRequestCount = 0;
                var terminalSnapshot = CreateReadySnapshot(
                    generations:
                        new UnityEditorGenerationSnapshot(1, 2, 3, 5));
                var handler = CreateHandler(
                    executionStore,
                    new MutableUnityEditorReadinessGate(terminalSnapshot),
                    checkpointStore,
                    assetRefreshController:
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
                    CreateCompileRequest(start),
                    CancellationToken.None);

                Assert.That(refreshRequestCount, Is.EqualTo(0));
                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcCompileErrorResponse payload,
                        out _),
                    Is.True);
                Assert.That(payload.Result, Is.Null);
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
                Assert.That(terminal.TerminalGeneration, Is.Not.Null);
                Assert.That(terminal.Verdict, Is.Null);
            });

        private static async ValueTask<LifecycleExecutionStartBinding> RegisterAsync (
            FileLifecycleExecutionStore executionStore,
            DateTimeOffset? deadlineUtc = null)
        {
            var definition =
                new LifecycleExecutionDefinition(LifecycleExecutionKind.Compile);
            var result = await executionStore.StartAsync(
                definition,
                Guid.NewGuid(),
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                ProjectIdentity,
                Host,
                CreateReadySnapshot().State.Generations,
                deadlineUtc ?? DateTimeOffset.UtcNow.AddSeconds(10),
                DateTimeOffset.UtcNow.AddSeconds(-1),
                CancellationToken.None);
            return result.Binding
                ?? throw new AssertionException(
                    $"Compile registration failed: {result.Outcome}");
        }

        private static async Task<StoredLifecycleExecution>
            WaitForExecutionStateAsync (
            FileLifecycleExecutionStore executionStore,
            Guid executionId,
            LifecycleExecutionState state)
        {
            var expectedState = TextVocabulary.GetText(state);
            while (true)
            {
                var stored = await executionStore.ReadAsync(
                    LifecycleExecutionKind.Compile,
                    executionId,
                    CancellationToken.None);
                if (stored != null
                    && string.Equals(
                        stored.CurrentReference.State.Value,
                        expectedState,
                        StringComparison.Ordinal))
                {
                    return stored;
                }

                await Task.Yield();
            }
        }

        private static async Task<CompileLifecycleExecutionCheckpoint>
            WaitForProviderReturnAsync (
            FileCompileLifecycleExecutionCheckpointStore checkpointStore,
            Guid executionId)
        {
            while (true)
            {
                var checkpoint = await checkpointStore.ReadAsync(
                    executionId,
                    CancellationToken.None);
                if (checkpoint?.ProviderReturnedAtUtc.HasValue == true)
                {
                    return checkpoint;
                }

                await Task.Yield();
            }
        }

        private static CompileHandlerFixture CreateHandler (
            FileLifecycleExecutionStore executionStore,
            MutableUnityEditorReadinessGate readinessGate,
            FileCompileLifecycleExecutionCheckpointStore checkpointStore = null,
            ImmediateUnityMutationLaneControl mutationLane = null,
            IUnityAssetRefreshController assetRefreshController = null,
            IDaemonLogger daemonLogger = null)
        {
            daemonLogger ??= NoOpDaemonLogger.Instance;
            var provider = new UnityEditorCompileLifecycleExecutionProvider(
                readinessGate,
                ProjectIdentity,
                new StubServerVersionProvider(ServerVersion),
                mutationLane ?? new ImmediateUnityMutationLaneControl(),
                assetRefreshController ?? new UnityAssetRefreshController(),
                new UnityEditorUpdateAwaiterAdapter());
            var executionHandler = new CompileLifecycleExecutionHandler(
                provider,
                daemonLogger,
                executionStore,
                checkpointStore
                    ?? new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore));
            return new CompileHandlerFixture(
                new CompileUnityIpcMethodHandler(
                    executionHandler,
                    daemonLogger),
                executionHandler);
        }

        private sealed class CompileHandlerFixture :
            IUnityIpcMethodHandler
        {
            private readonly CompileUnityIpcMethodHandler ipcHandler;
            private readonly CompileLifecycleExecutionHandler executionHandler;

            public CompileHandlerFixture (
                CompileUnityIpcMethodHandler ipcHandler,
                CompileLifecycleExecutionHandler executionHandler)
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

        private sealed class RecordingCompileLifecycleExecutionProvider :
            ICompileLifecycleExecutionProvider
        {
            private readonly UnityEditorRuntimeObservation observation;
            private readonly DateTimeOffset? delayFinalEvidenceUntilUtc;
            private readonly DateTimeOffset? delayMutationUntilUtc;
            private int lifecycleEvidenceCount;

            public RecordingCompileLifecycleExecutionProvider (
                UnityEditorRuntimeObservation observation,
                DateTimeOffset? delayFinalEvidenceUntilUtc = null,
                DateTimeOffset? delayMutationUntilUtc = null)
            {
                this.observation = observation;
                this.delayFinalEvidenceUntilUtc = delayFinalEvidenceUntilUtc;
                this.delayMutationUntilUtc = delayMutationUntilUtc;
            }

            public int RefreshRequestCount { get; private set; }

            public int MutationCount { get; private set; }

            public int DiagnosticsObservationCount { get; private set; }

            public int ObservationCount { get; private set; }

            public bool MutationCompleted { get; private set; }

            public UnityEditorRuntimeObservation CaptureObservation ()
            {
                ObservationCount++;
                return observation;
            }

            public UnityEditorObservation CreateLifecycleObservation (
                UnityEditorRuntimeObservation currentObservation)
            {
                return UnityLifecycleResponseFactory.Create(
                    ProjectIdentity,
                    ServerVersion,
                    currentObservation);
            }

            public CompileLifecycleResult.LifecycleEvidence
                CreateLifecycleEvidence (
                    UnityEditorRuntimeObservation currentObservation)
            {
                lifecycleEvidenceCount++;
                if (lifecycleEvidenceCount == 2
                    && delayFinalEvidenceUntilUtc.HasValue)
                {
                    WaitUntil(delayFinalEvidenceUntilUtc.Value);
                }

                return new CompileLifecycleResult.LifecycleEvidence(
                    ServerVersion,
                    ProjectIdentity.UnityVersion,
                    currentObservation.State,
                    currentObservation.ObservedAtUtc,
                    currentObservation.ActionRequired,
                    currentObservation.PrimaryDiagnostic);
            }

            private static void WaitUntil (DateTimeOffset utc)
            {
                while (DateTimeOffset.UtcNow < utc)
                {
                    Thread.Sleep(1);
                }
            }

            public IUnityMutationActivity BeginMutation ()
            {
                MutationCount++;
                if (delayMutationUntilUtc.HasValue)
                {
                    WaitUntil(delayMutationUntilUtc.Value);
                }

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

            public IDisposable BeginDiagnosticsObservation (
                ICompileLifecycleExecutionDiagnosticsSink diagnosticsSink)
            {
                Assert.That(diagnosticsSink, Is.Not.Null);
                DiagnosticsObservationCount++;
                return new EmptyDiagnosticsObservation();
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

            private sealed class EmptyDiagnosticsObservation :
                IDisposable
            {
                public void Dispose ()
                {
                }
            }
        }

        private sealed class ControlledCompileLifecycleExecutionProvider :
            ICompileLifecycleExecutionProvider
        {
            private readonly object gate = new();

            private UnityEditorRuntimeObservation observation;
            private TaskCompletionSource<bool> nextUpdate = CreateUpdateSource();
            private ICompileLifecycleExecutionDiagnosticsSink diagnosticsSink;
            private int refreshRequestCount;
            private int updateWaitCount;

            public ControlledCompileLifecycleExecutionProvider (
                UnityEditorRuntimeObservation observation)
            {
                this.observation = observation
                    ?? throw new ArgumentNullException(nameof(observation));
            }

            public int RefreshRequestCount
            {
                get
                {
                    lock (gate)
                    {
                        return refreshRequestCount;
                    }
                }
            }

            public int UpdateWaitCount
            {
                get
                {
                    lock (gate)
                    {
                        return updateWaitCount;
                    }
                }
            }

            public UnityEditorRuntimeObservation CaptureObservation ()
            {
                lock (gate)
                {
                    return observation;
                }
            }

            public UnityEditorObservation CreateLifecycleObservation (
                UnityEditorRuntimeObservation currentObservation)
            {
                return UnityLifecycleResponseFactory.Create(
                    ProjectIdentity,
                    ServerVersion,
                    currentObservation);
            }

            public CompileLifecycleResult.LifecycleEvidence
                CreateLifecycleEvidence (
                UnityEditorRuntimeObservation currentObservation)
            {
                return new CompileLifecycleResult.LifecycleEvidence(
                    ServerVersion,
                    ProjectIdentity.UnityVersion,
                    currentObservation.State,
                    currentObservation.ObservedAtUtc,
                    currentObservation.ActionRequired,
                    currentObservation.PrimaryDiagnostic);
            }

            public IUnityMutationActivity BeginMutation ()
            {
                return new EmptyMutationActivity();
            }

            public void RequestRefresh ()
            {
                lock (gate)
                {
                    refreshRequestCount++;
                }
            }

            public Task WaitForNextUpdateAsync (
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (gate)
                {
                    updateWaitCount++;
                    return nextUpdate.Task;
                }
            }

            public IDisposable BeginDiagnosticsObservation (
                ICompileLifecycleExecutionDiagnosticsSink diagnosticsSink)
            {
                this.diagnosticsSink = diagnosticsSink
                    ?? throw new ArgumentNullException(nameof(diagnosticsSink));
                return new EmptyDiagnosticsObservation();
            }

            public void Advance (UnityEditorRuntimeObservation nextObservation)
            {
                if (nextObservation == null)
                {
                    throw new ArgumentNullException(nameof(nextObservation));
                }

                TaskCompletionSource<bool> completedUpdate;
                lock (gate)
                {
                    observation = nextObservation;
                    completedUpdate = nextUpdate;
                    nextUpdate = CreateUpdateSource();
                }
                completedUpdate.TrySetResult(true);
            }

            public void StartCompilation (long batchId)
            {
                (diagnosticsSink
                    ?? throw new InvalidOperationException(
                        "Compile diagnostics observation has not started."))
                    .StartBatch(batchId);
            }

            public void CompleteCompilation (long batchId)
            {
                (diagnosticsSink
                    ?? throw new InvalidOperationException(
                        "Compile diagnostics observation has not started."))
                    .CompleteBatch(batchId);
            }

            private static TaskCompletionSource<bool> CreateUpdateSource ()
            {
                return new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            private sealed class EmptyMutationActivity : IUnityMutationActivity
            {
                public void Complete ()
                {
                }
            }

            private sealed class EmptyDiagnosticsObservation : IDisposable
            {
                public void Dispose ()
                {
                }
            }
        }

        private sealed class RejectingUnityAssetRefreshController :
            IUnityAssetRefreshController
        {
            private readonly Action beforeReject;

            public RejectingUnityAssetRefreshController (
                Action beforeReject = null)
            {
                this.beforeReject = beforeReject;
            }

            public void Refresh ()
            {
                beforeReject?.Invoke();
                throw new UnityAssetRefreshException(
                    "Unity rejected the asset refresh request.",
                    new InvalidOperationException("provider failure"));
            }
        }

        private static IpcRequestEnvelope CreateCompileRequest (
            LifecycleExecutionStartBinding start)
        {
            return new IpcRequestEnvelope(
                IpcProtocol.CurrentVersion,
                Guid.NewGuid(),
                "session-token",
                TextVocabulary.GetText(UnityIpcMethod.Compile),
                IpcPayloadCodec.SerializeToElement(new IpcCompileRequest(start)),
                "single",
                DateTimeOffset.UtcNow.AddSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
        }

        private static CompileLifecycleExecutionTerminalRecord ReadTerminalRecord (
            FileLifecycleExecutionStore executionStore,
            Guid executionId)
        {
            return LifecycleExecutionHandlerTestSupport.ReadTerminalRecord
                <CompileLifecycleExecutionTerminalRecord>(
                executionStore,
                LifecycleExecutionKind.Compile,
                executionId);
        }

        private static CompileLifecycleResult CreatePendingResult (
            UnityEditorRuntimeObservation before,
            bool requested = true)
        {
            var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            return new CompileLifecycleResult(
                new CompileLifecycleResult.RefreshEvidence(
                    CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
                    requested,
                    startedAtUtc,
                    CompletedAtUtc: null,
                    Completed: false),
                new CompileLifecycleResult.ScriptCompilationEvidence(
                    Started: false,
                    Completed: false,
                    before.State.Generations.CompileGeneration,
                    before.State.Generations.CompileGeneration,
                    new CompileLifecycleResult.DiagnosticsEvidence(0, 0, null)),
                new CompileLifecycleResult.DomainReloadEvidence(
                    ReloadRequired: false,
                    ReloadObserved: false,
                    before.State.Generations.DomainReloadGeneration,
                    before.State.Generations.DomainReloadGeneration,
                    Settled: false),
                new CompileLifecycleResult.LifecycleEvidence(
                    ServerVersion,
                    ProjectIdentity.UnityVersion,
                    before.State,
                    before.ObservedAtUtc,
                    before.ActionRequired,
                    before.PrimaryDiagnostic));
        }

        private static UnityEditorObservation CreateObservation (
            UnityEditorRuntimeObservation observation)
        {
            return UnityLifecycleResponseFactory.Create(
                ProjectIdentity,
                ServerVersion,
                observation);
        }

        private static UnityEditorRuntimeObservation CreateReadySnapshot (
            UnityEditorPrimaryDiagnostic primaryDiagnostic = null,
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
                DateTimeOffset.UtcNow,
                primaryDiagnostic);
        }

        private static UnityEditorRuntimeObservation CreateCompilingSnapshot ()
        {
            return new UnityEditorRuntimeObservation(
                new UnityEditorStateSnapshot(
                    UnityEditorMode.Batchmode,
                    UnityEditorLifecycleState.Compiling,
                    UnityEditorCompileState.Compiling,
                    new UnityEditorGenerationSnapshot(2, 2, 4, 4),
                    new UnityEditorPlayModeSnapshot(
                        UnityEditorPlayModeState.Stopped,
                        UnityEditorPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                DateTimeOffset.UtcNow,
                primaryDiagnostic: null);
        }

        private static UnityEditorRuntimeObservation CreateReimportingSnapshot ()
        {
            return new UnityEditorRuntimeObservation(
                new UnityEditorStateSnapshot(
                    UnityEditorMode.Batchmode,
                    UnityEditorLifecycleState.Reimporting,
                    UnityEditorCompileState.Ready,
                    new UnityEditorGenerationSnapshot(1, 2, 3, 4),
                    new UnityEditorPlayModeSnapshot(
                        UnityEditorPlayModeState.Stopped,
                        UnityEditorPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                DateTimeOffset.UtcNow,
                primaryDiagnostic: null);
        }

    }
}
