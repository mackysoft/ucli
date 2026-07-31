using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Cryptography;
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

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class LifecycleExecutionStartUnityIpcMethodHandlerTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("lifecycle-start-handler");

        private static readonly UnityProjectIdentity Project = new(
            ProjectPathTestValues.RepositoryUnityProject,
            ProjectFingerprint,
            "2023.2.22f1");

        private static readonly ProcessIdentity Process = new(42, 123);

        private static readonly Guid EditorInstanceId =
            Guid.Parse("5f84d6cc-61df-499c-a8bf-2c638cab7b32");

        private static readonly Guid EndpointGenerationId =
            Guid.Parse("b089439a-b988-4502-8c1f-827c50454b16");

        private static readonly Sha256Digest RefreshDefinitionDigest =
            LifecycleExecutionDefinitionDigest.Calculate(
                new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Refresh));

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenMatchingAdmissionPolicyAccepts_PersistsItsObservationBeforeReturningBinding () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = CreateExecutionStore(scope);
                var executionId = Guid.NewGuid();
                var startedAtUtc = DateTimeOffset.UtcNow;
                var deadlineUtc = startedAtUtc.AddMinutes(1);
                var lifetimeObserver =
                    new RecordingLifecycleExecutionHostLifetimeObserver();
                var deadlineScheduler =
                    new RecordingLifecycleExecutionDeadlineScheduler();
                var admissionPolicy =
                    StubLifecycleExecutionStartAdmissionPolicy.Accepted(
                        LifecycleExecutionKind.Refresh,
                        CreateObservation());
                var handler = CreateHandler(
                    executionStore,
                    lifetimeObserver,
                    deadlineScheduler,
                    admissionPolicy);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateRequest(
                        executionId,
                        RefreshDefinitionDigest,
                        startedAtUtc,
                        deadlineUtc),
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcLifecycleExecutionStartResponse payload,
                        out _),
                    Is.True);
                var stored = await executionStore.ReadAsync(
                    LifecycleExecutionKind.Refresh,
                    executionId,
                    CancellationToken.None);
                Assert.That(stored, Is.Not.Null);
                Assert.That(stored.IsTerminal, Is.False);
                Assert.That(stored.Start, Is.EqualTo(payload.Start));
                Assert.That(
                    stored.Start.StartedGeneration,
                    Is.EqualTo(admissionPolicy.Observation.State.Generations));
                Assert.That(admissionPolicy.CallCount, Is.EqualTo(1));
                Assert.That(
                    admissionPolicy.LastDeadlineUtc,
                    Is.EqualTo(deadlineUtc));
                Assert.That(lifetimeObserver.CallCount, Is.EqualTo(1));
                Assert.That(deadlineScheduler.CallCount, Is.EqualTo(1));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenMatchingAdmissionPolicyRejects_ReturnsItsErrorWithoutPersistingStart () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = CreateExecutionStore(scope);
                var executionId = Guid.NewGuid();
                var lifetimeObserver =
                    new RecordingLifecycleExecutionHostLifetimeObserver();
                var deadlineScheduler =
                    new RecordingLifecycleExecutionDeadlineScheduler();
                var admissionPolicy =
                    StubLifecycleExecutionStartAdmissionPolicy.Rejected(
                        LifecycleExecutionKind.Refresh,
                        new IpcError(
                            EditorLifecycleErrorCodes.EditorBusy,
                            "Unity Editor is busy.",
                            InstancePath: null));
                var handler = CreateHandler(
                    executionStore,
                    lifetimeObserver,
                    deadlineScheduler,
                    admissionPolicy);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateRequest(
                        executionId,
                        RefreshDefinitionDigest,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow.AddMinutes(1)),
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(response.Errors, Has.Count.EqualTo(1));
                Assert.That(
                    response.Errors[0].Code,
                    Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
                Assert.That(admissionPolicy.CallCount, Is.EqualTo(1));
                Assert.That(
                    await executionStore.ReadAsync(
                        LifecycleExecutionKind.Refresh,
                        executionId,
                        CancellationToken.None),
                    Is.Null);
                Assert.That(lifetimeObserver.CallCount, Is.EqualTo(0));
                Assert.That(deadlineScheduler.CallCount, Is.EqualTo(0));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenSameExecutionStartsConcurrently_AdmitsOnceAndReconnectsTheAuthoritativeStart () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = CreateExecutionStore(scope);
                var firstExecutionId = Guid.NewGuid();
                var unrelatedExecutionId = Guid.NewGuid();
                var startedAtUtc = DateTimeOffset.UtcNow;
                var deadlineUtc = startedAtUtc.AddMinutes(1);
                var conflictingDigest = Sha256Digest.Parse(new string('f', 64));
                var admissionPolicy =
                    new ControllableConcurrentStartAdmissionPolicy(
                        CreateObservation());
                var handler = CreateHandler(
                    executionStore,
                    new RecordingLifecycleExecutionHostLifetimeObserver(),
                    new RecordingLifecycleExecutionDeadlineScheduler(),
                    admissionPolicy);

                var firstResponseTask = UnityIpcMethodHandlerTestInvoker
                    .HandleAsync(
                        handler,
                        CreateRequest(
                            firstExecutionId,
                            RefreshDefinitionDigest,
                            startedAtUtc,
                            deadlineUtc),
                        CancellationToken.None)
                    .AsTask();
                await admissionPolicy.FirstAdmissionStarted;

                IpcResponse unrelatedResponse;
                Task<IpcResponse> reconnectResponseTask;
                Task<IpcResponse> conflictResponseTask;
                try
                {
                    unrelatedResponse = await UnityIpcMethodHandlerTestInvoker
                        .HandleAsync(
                            handler,
                            CreateRequest(
                                unrelatedExecutionId,
                                RefreshDefinitionDigest,
                                startedAtUtc,
                                deadlineUtc),
                            CancellationToken.None);
                    reconnectResponseTask = UnityIpcMethodHandlerTestInvoker
                        .HandleAsync(
                            handler,
                            CreateRequest(
                                firstExecutionId,
                                RefreshDefinitionDigest,
                                startedAtUtc,
                                deadlineUtc),
                            CancellationToken.None)
                        .AsTask();
                    conflictResponseTask = UnityIpcMethodHandlerTestInvoker
                        .HandleAsync(
                            handler,
                            CreateRequest(
                                firstExecutionId,
                                conflictingDigest,
                                startedAtUtc,
                                deadlineUtc),
                            CancellationToken.None)
                        .AsTask();

                    for (var index = 0; index < 5; index++)
                    {
                        await UniTask.Yield();
                    }

                    Assert.That(reconnectResponseTask.IsCompleted, Is.False);
                    Assert.That(conflictResponseTask.IsCompleted, Is.False);
                    Assert.That(admissionPolicy.CallCount, Is.EqualTo(2));
                }
                finally
                {
                    admissionPolicy.AcceptFirstAdmission();
                }

                var firstResponse = await firstResponseTask;
                var reconnectResponse = await reconnectResponseTask;
                var conflictResponse = await conflictResponseTask;

                Assert.That(unrelatedResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(firstResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(reconnectResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        firstResponse.Payload,
                        out IpcLifecycleExecutionStartResponse firstPayload,
                        out _),
                    Is.True);
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        reconnectResponse.Payload,
                        out IpcLifecycleExecutionStartResponse reconnectPayload,
                        out _),
                    Is.True);
                Assert.That(reconnectPayload.Start, Is.EqualTo(firstPayload.Start));
                Assert.That(conflictResponse.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(conflictResponse.Errors, Has.Count.EqualTo(1));
                Assert.That(
                    conflictResponse.Errors[0].Code,
                    Is.EqualTo(LifecycleExecutionErrorCodes.DefinitionConflict));
                Assert.That(admissionPolicy.CallCount, Is.EqualTo(2));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenExistingExecutionDeadlinePassed_ReconnectsSameStart () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = CreateExecutionStore(scope);
                var executionId = Guid.NewGuid();
                var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
                var deadlineUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
                var definition = new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Refresh);
                var established = await executionStore.StartAsync(
                    definition,
                    executionId,
                    LifecycleExecutionDefinitionDigest.Calculate(definition),
                    Project,
                    CreateHostRegistration(),
                    CreateObservation().State.Generations,
                    deadlineUtc,
                    startedAtUtc,
                    CancellationToken.None);
                Assert.That(established.IsSuccess, Is.True);
                var readinessGate =
                    StubUnityEditorReadinessGate.CreatePending();
                var admissionPolicy =
                    StubLifecycleExecutionStartAdmissionPolicy.Rejected(
                        LifecycleExecutionKind.Refresh,
                        new IpcError(
                            EditorLifecycleErrorCodes.EditorBusy,
                            "Unity Editor is busy.",
                            InstancePath: null));
                var handler = CreateHandler(
                    executionStore,
                    NoOpLifecycleExecutionHostLifetimeObserver.Instance,
                    new RecordingLifecycleExecutionDeadlineScheduler(),
                    admissionPolicy,
                    readinessGate);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateRequest(
                        executionId,
                        RefreshDefinitionDigest,
                        startedAtUtc,
                        deadlineUtc),
                    CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcLifecycleExecutionStartResponse payload,
                        out _),
                    Is.True);
                Assert.That(payload.Start, Is.EqualTo(established.Binding));
                Assert.That(admissionPolicy.CallCount, Is.EqualTo(0));
                Assert.That(readinessGate.CallCount, Is.EqualTo(0));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WhenTerminalExecutionIsRetriedFromAnotherHost_ReturnsHostMismatchWithoutRetracking () =>
            UniTask.ToCoroutine(async () =>
            {
                using var scope = TemporaryStorageScope.Create();
                var executionStore = CreateExecutionStore(scope);
                var executionId = Guid.NewGuid();
                var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
                var deadlineUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
                var definition = new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Refresh);
                var endpointRegistrationGenerationId = Guid.NewGuid();
                var established = await executionStore.StartAsync(
                    definition,
                    executionId,
                    LifecycleExecutionDefinitionDigest.Calculate(definition),
                    Project,
                    new LifecycleExecutionHostRegistration(
                        new ProcessIdentity(777, 888),
                        Guid.NewGuid(),
                        endpointRegistrationGenerationId,
                        endpointRegistrationGenerationId),
                    CreateObservation().State.Generations,
                    deadlineUtc,
                    startedAtUtc,
                    CancellationToken.None);
                Assert.That(established.IsSuccess, Is.True);
                var publication = await executionStore.PublishTerminalAsync(
                    new RefreshLifecycleExecutionTerminalRecord(
                        executionId,
                        established.Binding.LifecycleExecutionRef.DefinitionDigest,
                        established.Binding.Project,
                        established.Binding.Host,
                        established.Binding.StartedGeneration,
                        terminalGeneration: null,
                        established.Binding.DeadlineUtc,
                        established.Binding.StartedAtUtc,
                        established.Binding.DeadlineUtc,
                        LifecycleExecutionTerminalReason.DeadlineExceeded,
                        ExecutionApplicationState.NotApplied,
                        result: null,
                        verdict: null,
                        Array.Empty<ArtifactRef>()),
                    CancellationToken.None);
                Assert.That(publication.IsSuccess, Is.True);
                var stored = await executionStore.ReadAsync(
                    LifecycleExecutionKind.Refresh,
                    executionId,
                    CancellationToken.None);
                Assert.That(stored, Is.Not.Null);
                Assert.That(stored.IsTerminal, Is.True);
                var lifetimeObserver =
                    new RecordingLifecycleExecutionHostLifetimeObserver();
                var deadlineScheduler =
                    new RecordingLifecycleExecutionDeadlineScheduler();
                var readinessGate =
                    StubUnityEditorReadinessGate.CreatePending();
                var admissionPolicy =
                    StubLifecycleExecutionStartAdmissionPolicy.Accepted(
                        LifecycleExecutionKind.Refresh,
                        CreateObservation());
                var handler = CreateHandler(
                    executionStore,
                    lifetimeObserver,
                    deadlineScheduler,
                    admissionPolicy,
                    readinessGate);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateRequest(
                        executionId,
                        RefreshDefinitionDigest,
                        startedAtUtc,
                        deadlineUtc),
                    CancellationToken.None);

                Assert.That(
                    response.Status,
                    Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(response.Errors, Has.Count.EqualTo(1));
                Assert.That(
                    response.Errors[0].Code,
                    Is.EqualTo(LifecycleExecutionErrorCodes.HostMismatch));
                Assert.That(admissionPolicy.CallCount, Is.EqualTo(0));
                Assert.That(readinessGate.CallCount, Is.EqualTo(0));
                Assert.That(lifetimeObserver.CallCount, Is.EqualTo(0));
                Assert.That(deadlineScheduler.CallCount, Is.EqualTo(0));
            });

        private static LifecycleExecutionStartUnityIpcMethodHandler CreateHandler (
            FileLifecycleExecutionStore executionStore,
            ILifecycleExecutionHostLifetimeObserver lifetimeObserver,
            ILifecycleExecutionDeadlineScheduler deadlineScheduler,
            ILifecycleExecutionStartAdmissionPolicy admissionPolicy,
            IUnityEditorReadinessGate runtimeObservationSource = null)
        {
            return new LifecycleExecutionStartUnityIpcMethodHandler(
                executionStore,
                Project,
                new UnityLifecycleExecutionHostContext(
                    Process,
                    EditorInstanceId,
                    EndpointGenerationId,
                    recoveryLease: null),
                runtimeObservationSource
                    ?? new StubUnityEditorReadinessGate(),
                new ILifecycleExecutionStartAdmissionPolicy[]
                {
                    admissionPolicy,
                },
                lifetimeObserver,
                deadlineScheduler,
                NoOpDaemonLogger.Instance);
        }

        private static IpcRequestEnvelope CreateRequest (
            Guid executionId,
            Sha256Digest definitionDigest,
            DateTimeOffset startedAtUtc,
            DateTimeOffset deadlineUtc)
        {
            return new IpcRequestEnvelope(
                IpcProtocol.CurrentVersion,
                Guid.NewGuid(),
                "session-token",
                TextVocabulary.GetText(UnityIpcMethod.LifecycleStart),
                IpcPayloadCodec.SerializeToElement(
                    new IpcLifecycleExecutionStartRequest(
                        LifecycleExecutionKind.Refresh,
                        executionId,
                        definitionDigest,
                        deadlineUtc,
                        startedAtUtc)),
                "single",
                DateTimeOffset.UtcNow.AddSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
        }

        private static FileLifecycleExecutionStore CreateExecutionStore (
            TemporaryStorageScope scope)
        {
            return new FileLifecycleExecutionStore(
                AbsolutePath.Parse(scope.RootPath),
                ProjectFingerprint);
        }

        private static LifecycleExecutionHostRegistration CreateHostRegistration ()
        {
            return new LifecycleExecutionHostRegistration(
                Process,
                EditorInstanceId,
                EndpointGenerationId,
                EndpointGenerationId);
        }

        private static UnityEditorRuntimeObservation CreateObservation ()
        {
            return new UnityEditorRuntimeObservation(
                new UnityEditorStateSnapshot(
                    UnityEditorMode.Batchmode,
                    UnityEditorLifecycleState.Ready,
                    UnityEditorCompileState.Ready,
                    new UnityEditorGenerationSnapshot(1, 2, 3, 4),
                    new UnityEditorPlayModeSnapshot(
                        UnityEditorPlayModeState.Stopped,
                        UnityEditorPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                DateTimeOffset.UtcNow);
        }

        private sealed class RecordingLifecycleExecutionHostLifetimeObserver :
            ILifecycleExecutionHostLifetimeObserver
        {
            public int CallCount { get; private set; }

            public DateTimeOffset? LastDeadlineUtc { get; private set; }

            public void OnStartAccepted (DateTimeOffset deadlineUtc)
            {
                CallCount++;
                LastDeadlineUtc = deadlineUtc;
            }
        }

        private sealed class RecordingLifecycleExecutionDeadlineScheduler :
            ILifecycleExecutionDeadlineScheduler
        {
            public int CallCount { get; private set; }

            public LifecycleExecutionKind? LastKind { get; private set; }

            public Guid? LastExecutionId { get; private set; }

            public void Track (
                LifecycleExecutionKind kind,
                Guid executionId)
            {
                CallCount++;
                LastKind = kind;
                LastExecutionId = executionId;
            }
        }

        private sealed class StubLifecycleExecutionStartAdmissionPolicy :
            ILifecycleExecutionStartAdmissionPolicy
        {
            private readonly LifecycleExecutionStartAdmission admission;

            private StubLifecycleExecutionStartAdmissionPolicy (
                LifecycleExecutionKind kind,
                LifecycleExecutionStartAdmission admission)
            {
                Kind = kind;
                this.admission = admission;
            }

            public LifecycleExecutionKind Kind { get; }

            public int CallCount { get; private set; }

            public DateTimeOffset? LastDeadlineUtc { get; private set; }

            public UnityEditorRuntimeObservation Observation =>
                admission.Observation;

            public static StubLifecycleExecutionStartAdmissionPolicy Accepted (
                LifecycleExecutionKind kind,
                UnityEditorRuntimeObservation observation)
            {
                return new StubLifecycleExecutionStartAdmissionPolicy(
                    kind,
                    LifecycleExecutionStartAdmission.Accepted(observation));
            }

            public static StubLifecycleExecutionStartAdmissionPolicy Rejected (
                LifecycleExecutionKind kind,
                IpcError error)
            {
                return new StubLifecycleExecutionStartAdmissionPolicy(
                    kind,
                    LifecycleExecutionStartAdmission.Rejected(error));
            }

            public ValueTask<LifecycleExecutionStartAdmission> AdmitAsync (
                DateTimeOffset deadlineUtc,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                LastDeadlineUtc = deadlineUtc;
                return new ValueTask<LifecycleExecutionStartAdmission>(admission);
            }
        }

        private sealed class ControllableConcurrentStartAdmissionPolicy :
            ILifecycleExecutionStartAdmissionPolicy
        {
            private readonly UnityEditorRuntimeObservation observation;

            private readonly TaskCompletionSource<bool> firstAdmissionStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<LifecycleExecutionStartAdmission>
                firstAdmission =
                    new(TaskCreationOptions.RunContinuationsAsynchronously);

            private int callCount;

            public ControllableConcurrentStartAdmissionPolicy (
                UnityEditorRuntimeObservation observation)
            {
                this.observation = observation
                    ?? throw new ArgumentNullException(nameof(observation));
            }

            public LifecycleExecutionKind Kind =>
                LifecycleExecutionKind.Refresh;

            public int CallCount => Volatile.Read(ref callCount);

            public Task FirstAdmissionStarted => firstAdmissionStarted.Task;

            public void AcceptFirstAdmission ()
            {
                firstAdmission.TrySetResult(
                    LifecycleExecutionStartAdmission.Accepted(observation));
            }

            public async ValueTask<LifecycleExecutionStartAdmission> AdmitAsync (
                DateTimeOffset deadlineUtc,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentCall = Interlocked.Increment(ref callCount);
                if (currentCall == 1)
                {
                    firstAdmissionStarted.TrySetResult(true);
                    return await firstAdmission.Task;
                }
                if (currentCall == 2)
                {
                    return LifecycleExecutionStartAdmission.Accepted(observation);
                }

                return LifecycleExecutionStartAdmission.Rejected(
                    new IpcError(
                        EditorLifecycleErrorCodes.EditorBusy,
                        "A concurrent admission reached the action policy.",
                        InstancePath: null));
            }
        }

        private sealed class TemporaryStorageScope : IDisposable
        {
            private TemporaryStorageScope (string rootPath)
            {
                RootPath = rootPath;
            }

            public string RootPath { get; }

            public static TemporaryStorageScope Create ()
            {
                var rootPath = Path.Combine(
                    Path.GetTempPath(),
                    "ucli-lifecycle-start-handler-tests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(rootPath);
                return new TemporaryStorageScope(rootPath);
            }

            public void Dispose ()
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
        }
    }
}
