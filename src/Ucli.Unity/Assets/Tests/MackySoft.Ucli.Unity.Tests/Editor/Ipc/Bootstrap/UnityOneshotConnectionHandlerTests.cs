using System;
using System.Collections;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;
using static MackySoft.Ucli.Unity.Tests.LifecycleExecutionHandlerTestSupport;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityOneshotConnectionHandlerTests
    {
        private static readonly AbsolutePath StorageRoot = AbsolutePath.Parse(
            Path.Combine(Path.GetTempPath(), "oneshot-connection-handler-tests"));

        private static readonly ProjectFingerprint ProjectFingerprint = new(
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

        private static readonly UnityProjectIdentity Project = new(
            "/ucli/oneshot-connection-handler-tests",
            ProjectFingerprint,
            "2023.2.22f1");

        private static readonly DateTimeOffset ObservedUtc =
            new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

        private static readonly TimeSpan WatchdogPollInterval = TimeSpan.FromMilliseconds(10);

        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenOneshotStartupPingHandled_KeepsRequestDeadlineAndDoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            var requestExitTimeout = TimeSpan.FromMinutes(1);
            var elapsedTicks = 0L;
            var exitObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc + requestExitTimeout),
                pollInterval: WatchdogPollInterval,
                parentProcessIsSameProcess: static _ => true,
                observedUtcNow: ObservedUtc,
                monotonicClock: new DelegatingMonotonicClock(
                    () => new TimeSpan(Interlocked.Read(ref elapsedTicks))),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () => exitObserved.TrySetResult(true));
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(
                UnityIpcMethod.Ping,
                JsonSerializer.SerializeToElement(new IpcPingRequest(IpcPingClientVersions.OneshotStartup)));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal, watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.Ping));
            Assert.That(completionSignal.IsCompleted, Is.False);
            Interlocked.Exchange(ref elapsedTicks, requestExitTimeout.Ticks);
            Assert.That(exitObserved.Task.Wait(SignalWaitTimeout), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenReadyPingRequestHandled_ReleasesRequestDeadlineWithoutSignalingCompletion () => UniTask.ToCoroutine(async () =>
        {
            var requestExitTimeout = TimeSpan.FromMinutes(1);
            var elapsedTicks = 0L;
            var parentProbeAfterDeadline = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var exitCount = 0;
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc + requestExitTimeout),
                pollInterval: WatchdogPollInterval,
                parentProcessIsSameProcess: _ =>
                {
                    if (Interlocked.Read(ref elapsedTicks) >= requestExitTimeout.Ticks)
                    {
                        parentProbeAfterDeadline.TrySetResult(true);
                    }

                    return true;
                },
                observedUtcNow: ObservedUtc,
                monotonicClock: new DelegatingMonotonicClock(
                    () => new TimeSpan(Interlocked.Read(ref elapsedTicks))),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () => Interlocked.Increment(ref exitCount));
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(UnityIpcMethod.Ping, JsonSerializer.SerializeToElement(new IpcPingRequest(IpcPingClientVersions.Ready)));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal, watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.Ping));
            Assert.That(completionSignal.IsCompleted, Is.False);
            Interlocked.Exchange(ref elapsedTicks, requestExitTimeout.Ticks);
            Assert.That(parentProbeAfterDeadline.Task.Wait(SignalWaitTimeout), Is.True);
            Assert.That(Volatile.Read(ref exitCount), Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenSuccessfulNonPingRequestHandled_DisablesRequestDeadlineBeforeSignalingCompletion () => UniTask.ToCoroutine(async () =>
        {
            var requestExitTimeout = TimeSpan.FromMinutes(1);
            var elapsedTicks = 0L;
            var parentProbeAfterDeadline = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var exitCount = 0;
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc + requestExitTimeout),
                pollInterval: WatchdogPollInterval,
                parentProcessIsSameProcess: _ =>
                {
                    if (Interlocked.Read(ref elapsedTicks) >= requestExitTimeout.Ticks)
                    {
                        parentProbeAfterDeadline.TrySetResult(true);
                    }

                    return true;
                },
                observedUtcNow: ObservedUtc,
                monotonicClock: new DelegatingMonotonicClock(
                    () => new TimeSpan(Interlocked.Read(ref elapsedTicks))),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () => Interlocked.Increment(ref exitCount));
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(UnityIpcMethod.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal, watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.True);
            Interlocked.Exchange(ref elapsedTicks, requestExitTimeout.Ticks);
            Assert.That(parentProbeAfterDeadline.Task.Wait(SignalWaitTimeout), Is.True);
            Assert.That(Volatile.Read(ref exitCount), Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenEvalPlanThenCallComplete_KeepsHostForPlanAndSignalsAfterCall () => UniTask.ToCoroutine(async () =>
        {
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var planRequest = CreateRequest(
                UnityIpcMethod.EvalPlan,
                JsonSerializer.SerializeToElement(new IpcEvalPlanRequest(
                    "context.DeclareNoChanges();",
                    CsEvalSourceKind.Snippet,
                    allowDangerous: true,
                    allowPlayMode: false)));
            var planHandler = CreateHandler(
                planRequest,
                CreateSuccessResponse(planRequest.RequestId),
                completionSignal,
                watchdog);

            using (var planStream = await CreateStreamAsync(planRequest))
            {
                var planResult = await planHandler.HandleAsync(planStream, CancellationToken.None);
                Assert.That(planResult.Method, Is.EqualTo(UnityIpcMethod.EvalPlan));
                Assert.That(completionSignal.IsCompleted, Is.False);
            }

            var callRequest = CreateRequest(
                UnityIpcMethod.EvalCall,
                JsonSerializer.SerializeToElement(new IpcEvalCallRequest(
                    "context.DeclareNoChanges();",
                    CsEvalSourceKind.Snippet,
                    allowDangerous: true,
                    allowPlayMode: false,
                    planToken: "test-plan-token")));
            var callHandler = CreateHandler(
                callRequest,
                CreateSuccessResponse(callRequest.RequestId),
                completionSignal,
                watchdog);

            using var callStream = await CreateStreamAsync(callRequest);
            var callResult = await callHandler.HandleAsync(callStream, CancellationToken.None);

            Assert.That(callResult.Method, Is.EqualTo(UnityIpcMethod.EvalCall));
            Assert.That(completionSignal.IsCompleted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenLifecycleStartWasAcceptedButResponseWriteFails_IgnoresParentLoss () => UniTask.ToCoroutine(async () =>
        {
            var lifecycleExecutionTimeout = TimeSpan.FromMinutes(2);
            var lifecycleInspectionCount = 0;
            var lifecycleInspectionObserved = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var exitObserved = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var parentIsAlive = 1;
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(
                    ObservedUtc + lifecycleExecutionTimeout + TimeSpan.FromSeconds(3)),
                pollInterval: WatchdogPollInterval,
                parentProcessIsSameProcess: _ => Volatile.Read(ref parentIsAlive) != 0,
                observedUtcNow: ObservedUtc,
                monotonicClock: new DelegatingMonotonicClock(() =>
                {
                    if (Volatile.Read(ref parentIsAlive) == 0
                        && Interlocked.Increment(
                            ref lifecycleInspectionCount) >= 2)
                    {
                        lifecycleInspectionObserved.TrySetResult(true);
                    }

                    return TimeSpan.Zero;
                }),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () => exitObserved.TrySetResult(true));
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var definition = new LifecycleExecutionDefinition(
                LifecycleExecutionKind.Refresh);
            var request = CreateRequest(
                UnityIpcMethod.LifecycleStart,
                IpcPayloadCodec.SerializeToElement(
                    new IpcLifecycleExecutionStartRequest(
                        definition.Kind,
                        Guid.Parse("0e13dc98-a0fd-4c44-8b48-43ee910618b8"),
                        LifecycleExecutionDefinitionDigest.Calculate(definition),
                        ObservedUtc + lifecycleExecutionTimeout,
                        ObservedUtc)));
            var handler = CreateHandler(
                request,
                CreateSuccessResponse(request.RequestId),
                completionSignal,
                watchdog,
                beforeResponse: _ =>
                {
                    ((ILifecycleExecutionHostLifetimeObserver)watchdog)
                        .OnStartAccepted(
                            ObservedUtc + lifecycleExecutionTimeout);
                });

            using var stream = await CreateResponseWriteFailureStreamAsync(
                request);
            var handledResult = await handler.HandleAsync(
                stream,
                CancellationToken.None);

            Assert.That(handledResult.HasTerminalResponse, Is.False);
            Assert.That(completionSignal.IsCompleted, Is.False);

            Volatile.Write(ref parentIsAlive, 0);
            Assert.That(
                lifecycleInspectionObserved.Task.Wait(SignalWaitTimeout),
                Is.True);
            Assert.That(exitObserved.Task.IsCompleted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenSuccessfulNonLifecycleStartCompletes_StillExitsAfterParentLoss () => UniTask.ToCoroutine(async () =>
        {
            var parentIsAlive = 1;
            var exitObserved = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc.AddMinutes(1)),
                pollInterval: WatchdogPollInterval,
                parentProcessIsSameProcess: _ => Volatile.Read(ref parentIsAlive) != 0,
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () => exitObserved.TrySetResult(true));
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(
                UnityIpcMethod.OpsRead,
                JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(
                request,
                CreateSuccessResponse(request.RequestId),
                completionSignal,
                watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(
                stream,
                CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.True);

            Volatile.Write(ref parentIsAlive, 0);
            Assert.That(exitObserved.Task.Wait(SignalWaitTimeout), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenShutdownRequestHandled_SignalsCompletion () => UniTask.ToCoroutine(async () =>
        {
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(UnityIpcMethod.Shutdown, JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal, watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.Shutdown));
            Assert.That(handledResult.IsShutdownAdmissionCommitted, Is.True);
            Assert.That(completionSignal.IsCompleted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenShutdownRequestIsRejectedAsEditorBusy_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(
                UnityIpcMethod.Shutdown,
                JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")));
            var handler = CreateHandler(
                request,
                CreateErrorResponse(request.RequestId, EditorLifecycleErrorCodes.EditorBusy),
                completionSignal,
                watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.Shutdown));
            Assert.That(handledResult.IsShutdownAdmissionCommitted, Is.False);
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenNonPingRequestReturnsError_SignalsCompletion () => UniTask.ToCoroutine(async () =>
        {
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(UnityIpcMethod.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var errorResponse = CreateErrorResponse(request.RequestId, UcliCoreErrorCodes.InvalidArgument);
            var handler = CreateHandler(request, errorResponse, completionSignal, watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.True);
        });

        [TestCase(LifecycleExecutionKind.Refresh)]
        [TestCase(LifecycleExecutionKind.Compile)]
        [TestCase(LifecycleExecutionKind.PlayEnter)]
        [TestCase(LifecycleExecutionKind.PlayExit)]
        [Category("Size.Small")]
        public async Task HandleAsync_WhenLifecycleActionRemainsPublishing_DoesNotSignalCompletion (
            LifecycleExecutionKind kind)
        {
            using var storageScope = TemporaryStorageScope.Create();
            var executionStore = storageScope.CreateExecutionStore();
            var start = await StartExecutionAsync(executionStore, kind);
            var terminalPath = executionStore.Paths.ResolveTerminalRecordPath(
                kind,
                start.LifecycleExecutionRef.Id).Target;
            await WriteGuardedTextAsync(
                terminalPath,
                "invalid-terminal-record",
                CancellationToken.None);
            var publication = await executionStore.PublishTerminalAsync(
                CreateDeadlineTerminalRecord(kind, start),
                CancellationToken.None);
            Assert.That(
                publication.Outcome,
                Is.EqualTo(
                    LifecycleExecutionTerminalPublicationOutcome
                        .PublicationFailed));
            Assert.That(publication.TerminalReference, Is.Null);
            Assert.That(publication.Failure, Is.TypeOf<IOException>());
            var publishing = await executionStore.ReadAsync(
                kind,
                start.LifecycleExecutionRef.Id,
                CancellationToken.None);
            Assert.That(publishing.IsTerminal, Is.False);
            Assert.That(
                publishing.CurrentReference.Lifecycle,
                Is.EqualTo(ExecutionLifecycle.Recovery));
            Assert.That(
                publishing.CurrentReference.State.Value,
                Is.EqualTo(TextVocabulary.GetText(
                    LifecycleExecutionState.Publishing)));
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateLifecycleActionRequest(kind, start);
            var handler = CreateHandler(
                request,
                CreateErrorResponse(
                    request.RequestId,
                    LifecycleExecutionErrorCodes.TerminalPublicationFailed),
                completionSignal,
                watchdog,
                executionStore: executionStore);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(
                stream,
                CancellationToken.None);

            Assert.That(handledResult.HasTerminalResponse, Is.True);
            Assert.That(completionSignal.IsCompleted, Is.False);
        }

        [TestCase(LifecycleExecutionKind.Refresh)]
        [TestCase(LifecycleExecutionKind.Compile)]
        [TestCase(LifecycleExecutionKind.PlayEnter)]
        [TestCase(LifecycleExecutionKind.PlayExit)]
        [Category("Size.Small")]
        public async Task HandleAsync_WhenLifecycleActionHasDurableTerminal_SignalsCompletion (
            LifecycleExecutionKind kind)
        {
            using var storageScope = TemporaryStorageScope.Create();
            var executionStore = storageScope.CreateExecutionStore();
            var start = await StartExecutionAsync(executionStore, kind);
            var publication = await executionStore.PublishTerminalAsync(
                CreateDeadlineTerminalRecord(kind, start),
                CancellationToken.None);
            Assert.That(publication.IsSuccess, Is.True);
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateLifecycleActionRequest(kind, start);
            var handler = CreateHandler(
                request,
                CreateSuccessResponse(request.RequestId),
                completionSignal,
                watchdog,
                executionStore: executionStore);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(
                stream,
                CancellationToken.None);

            Assert.That(handledResult.HasTerminalResponse, Is.True);
            Assert.That(completionSignal.IsCompleted, Is.True);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenSessionTokenFailureHandled_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(UnityIpcMethod.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(
                request,
                CreateErrorResponse(request.RequestId, IpcSessionErrorCodes.SessionTokenInvalid),
                completionSignal,
                watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenProtocolMismatchHandled_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(UnityIpcMethod.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(
                request,
                CreateErrorResponse(request.RequestId, IpcProtocolErrorCodes.ProtocolVersionMismatch),
                completionSignal,
                watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        private static OneshotProcessLifetimeWatchdog CreateIdleWatchdog ()
        {
            return new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc.AddMinutes(1)),
                pollInterval: WatchdogPollInterval,
                parentProcessIsSameProcess: static _ => true,
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: static () => { });
        }

        private static IpcOneshotBootstrapEnvelope CreateBootstrapEnvelope (DateTimeOffset exitDeadlineUtc)
        {
            return new IpcOneshotBootstrapEnvelope(
                BootstrapId: Guid.Parse("a23a9990-eed2-4e94-b892-9c7d5609eab4"),
                ParentProcess: new ProcessIdentity(42, 123),
                ProjectFingerprint: new ProjectFingerprint(
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
                SessionToken: IpcSessionToken.CreateRandom(),
                CreatedAtUtc: ObservedUtc.AddMinutes(-10),
                ExitDeadlineUtc: exitDeadlineUtc,
                Endpoint: new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-oneshot-connection-handler-tests"));
        }

        private static UnityOneshotConnectionHandler CreateHandler (
            IpcRequestEnvelope expectedRequest,
            IpcResponse response,
            OneshotRequestCompletionSignal completionSignal,
            OneshotProcessLifetimeWatchdog lifetimeWatchdog,
            Action<ValidatedUnityIpcRequest> beforeResponse = null,
            FileLifecycleExecutionStore executionStore = null)
        {
            return new UnityOneshotConnectionHandler(
                new UnityIpcConnectionHandler(
                    requestHandler: new StubRequestHandler(
                        expectedRequest,
                        response,
                        beforeResponse),
                    shutdownAdmissionCoordinator: new PreparedShutdownAdmissionCoordinator(expectedRequest),
                    phaseScopeFactory: new IpcRequestPhaseScopeFactory(),
                    initialFrameReadTimeout: UnityIpcConnectionHandler.DefaultInitialFrameReadTimeout,
                    responseFrameWriteTimeout: UnityIpcConnectionHandler.DefaultResponseFrameWriteTimeout),
                completionSignal,
                lifetimeWatchdog,
                executionStore
                    ?? new FileLifecycleExecutionStore(
                        AbsolutePath.Parse(
                            Path.Combine(
                                Path.GetTempPath(),
                                "ucli-oneshot-connection-handler-empty-store",
                                Guid.NewGuid().ToString("N"))),
                        ProjectFingerprint));
        }

        private static async Task<LifecycleExecutionStartBinding>
            StartExecutionAsync (
                FileLifecycleExecutionStore executionStore,
                LifecycleExecutionKind kind)
        {
            var definition = new LifecycleExecutionDefinition(kind);
            var endpointRegistrationGenerationId = Guid.NewGuid();
            var started = await executionStore.StartAsync(
                definition,
                Guid.NewGuid(),
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                Project,
                new LifecycleExecutionHostRegistration(
                    new ProcessIdentity(42, 123),
                    Guid.NewGuid(),
                    endpointRegistrationGenerationId,
                    endpointRegistrationGenerationId),
                new UnityEditorGenerationSnapshot(1, 1, 1, 1),
                ObservedUtc.AddMinutes(5),
                ObservedUtc,
                CancellationToken.None);
            Assert.That(started.IsSuccess, Is.True);
            return started.Binding;
        }

        private static IpcRequestEnvelope CreateLifecycleActionRequest (
            LifecycleExecutionKind kind,
            LifecycleExecutionStartBinding start)
        {
            var method = kind switch
            {
                LifecycleExecutionKind.Refresh => UnityIpcMethod.Refresh,
                LifecycleExecutionKind.Compile => UnityIpcMethod.Compile,
                LifecycleExecutionKind.PlayEnter => UnityIpcMethod.PlayEnter,
                LifecycleExecutionKind.PlayExit => UnityIpcMethod.PlayExit,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Unsupported Lifecycle Execution kind."),
            };
            var payload = kind switch
            {
                LifecycleExecutionKind.Refresh =>
                    IpcPayloadCodec.SerializeToElement(
                        new IpcRefreshRequest(start)),
                LifecycleExecutionKind.Compile =>
                    IpcPayloadCodec.SerializeToElement(
                        new IpcCompileRequest(start)),
                LifecycleExecutionKind.PlayEnter =>
                    IpcPayloadCodec.SerializeToElement(
                        new IpcPlayEnterRequest(start)),
                LifecycleExecutionKind.PlayExit =>
                    IpcPayloadCodec.SerializeToElement(
                        new IpcPlayExitRequest(start)),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Unsupported Lifecycle Execution kind."),
            };
            return CreateRequest(method, payload);
        }

        private static LifecycleExecutionTerminalRecord
            CreateDeadlineTerminalRecord (
                LifecycleExecutionKind kind,
                LifecycleExecutionStartBinding start)
        {
            return kind switch
            {
                LifecycleExecutionKind.Refresh =>
                    new RefreshLifecycleExecutionTerminalRecord(
                        start.LifecycleExecutionRef.Id,
                        start.LifecycleExecutionRef.DefinitionDigest,
                        start.Project,
                        start.Host,
                        start.StartedGeneration,
                        terminalGeneration: null,
                        start.DeadlineUtc,
                        start.StartedAtUtc,
                        start.DeadlineUtc,
                        LifecycleExecutionTerminalReason.DeadlineExceeded,
                        ExecutionApplicationState.NotApplied,
                        result: null,
                        verdict: null,
                        Array.Empty<ArtifactRef>()),
                LifecycleExecutionKind.Compile =>
                    new CompileLifecycleExecutionTerminalRecord(
                        start.LifecycleExecutionRef.Id,
                        start.LifecycleExecutionRef.DefinitionDigest,
                        start.Project,
                        start.Host,
                        start.StartedGeneration,
                        terminalGeneration: null,
                        start.DeadlineUtc,
                        start.StartedAtUtc,
                        start.DeadlineUtc,
                        LifecycleExecutionTerminalReason.DeadlineExceeded,
                        ExecutionApplicationState.NotApplied,
                        result: null,
                        verdict: null,
                        Array.Empty<ArtifactRef>()),
                LifecycleExecutionKind.PlayEnter =>
                    new PlayEnterLifecycleExecutionTerminalRecord(
                        start.LifecycleExecutionRef.Id,
                        start.LifecycleExecutionRef.DefinitionDigest,
                        start.Project,
                        start.Host,
                        start.StartedGeneration,
                        terminalGeneration: null,
                        start.DeadlineUtc,
                        start.StartedAtUtc,
                        start.DeadlineUtc,
                        LifecycleExecutionTerminalReason.DeadlineExceeded,
                        ExecutionApplicationState.NotApplied,
                        result: null,
                        verdict: null,
                        Array.Empty<ArtifactRef>()),
                LifecycleExecutionKind.PlayExit =>
                    new PlayExitLifecycleExecutionTerminalRecord(
                        start.LifecycleExecutionRef.Id,
                        start.LifecycleExecutionRef.DefinitionDigest,
                        start.Project,
                        start.Host,
                        start.StartedGeneration,
                        terminalGeneration: null,
                        start.DeadlineUtc,
                        start.StartedAtUtc,
                        start.DeadlineUtc,
                        LifecycleExecutionTerminalReason.DeadlineExceeded,
                        ExecutionApplicationState.NotApplied,
                        result: null,
                        verdict: null,
                        Array.Empty<ArtifactRef>()),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Unsupported Lifecycle Execution kind."),
            };
        }

        private sealed class PreparedShutdownAdmissionCoordinator : IUnityShutdownAdmissionCoordinator
        {
            private readonly Guid preparedRequestId;

            public PreparedShutdownAdmissionCoordinator (IpcRequestEnvelope preparedRequest)
            {
                preparedRequestId = (preparedRequest ?? throw new ArgumentNullException(nameof(preparedRequest))).RequestId;
            }

            public bool TryPrepare (ValidatedUnityIpcRequest request, out string errorMessage)
            {
                errorMessage = null;
                return request != null && request.RequestId == preparedRequestId;
            }

            public bool TryCommit (ValidatedUnityIpcRequest request)
            {
                return request != null && request.RequestId == preparedRequestId;
            }

            public void Abort (ValidatedUnityIpcRequest request)
            {
            }
        }

        private static async Task<MemoryStream> CreateStreamAsync (IpcRequestEnvelope request)
        {
            var stream = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            stream.Position = 0;
            return stream;
        }

        private static async Task<Stream> CreateResponseWriteFailureStreamAsync (
            IpcRequestEnvelope request)
        {
            using var requestStream = await CreateStreamAsync(request);
            return new ResponseWriteFailureMemoryStream(
                requestStream.ToArray());
        }

        private static IpcRequestEnvelope CreateRequest (
            UnityIpcMethod method,
            JsonElement payload)
        {
            return new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "oneshot",
                method: TextVocabulary.GetText(method),
                payload: payload,
                responseMode: "single",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
        }

        private sealed class StubRequestHandler : IUnityIpcRequestHandler
        {
            private readonly IpcRequestEnvelope expectedRequest;

            private readonly IpcResponse response;

            private readonly Action<ValidatedUnityIpcRequest> beforeResponse;

            public StubRequestHandler (
                IpcRequestEnvelope expectedRequest,
                IpcResponse response,
                Action<ValidatedUnityIpcRequest> beforeResponse)
            {
                this.expectedRequest = expectedRequest;
                this.response = response;
                this.beforeResponse = beforeResponse;
            }

            public Task<UnityIpcRequestValidationResult> ValidateAsync (
                IpcRequestEnvelope request,
                IpcRequestPhaseScope phaseScope)
            {
                phaseScope.ExecutionCancellation.Token.ThrowIfCancellationRequested();
                Assert.That(request.Method, Is.EqualTo(expectedRequest.Method));
                Assert.That(request.RequestId, Is.EqualTo(expectedRequest.RequestId));
                return Task.FromResult(ValidatedUnityIpcRequestTestFactory.Success(request));
            }

            public Task<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestPhaseScope phaseScope)
            {
                phaseScope.ExecutionCancellation.Token.ThrowIfCancellationRequested();
                Assert.That(request.Method, Is.Not.EqualTo(0));
                Assert.That(request.ResponseMode, Is.EqualTo(IpcResponseMode.Single));
                beforeResponse?.Invoke(request);
                return Task.FromResult(response);
            }

            public Task<IpcResponse> HandleStreamingAsync (
                ValidatedUnityIpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                IpcRequestPhaseScope phaseScope)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ResponseWriteFailureMemoryStream : MemoryStream
        {
            public ResponseWriteFailureMemoryStream (byte[] requestBytes)
                : base(requestBytes, writable: true)
            {
            }

            public override void Write (
                byte[] buffer,
                int offset,
                int count)
            {
                throw new IOException("response write failed");
            }

            public override Task WriteAsync (
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                return Task.FromException(
                    new IOException("response write failed"));
            }

            public override ValueTask WriteAsync (
                ReadOnlyMemory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                return new ValueTask(
                    Task.FromException(
                        new IOException("response write failed")));
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
                    "ucli-oneshot-connection-handler-tests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(rootPath);
                return new TemporaryStorageScope(rootPath);
            }

            public FileLifecycleExecutionStore CreateExecutionStore ()
            {
                return new FileLifecycleExecutionStore(
                    AbsolutePath.Parse(RootPath),
                    ProjectFingerprint);
            }

            public void Dispose ()
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
        }

        private static IpcResponse CreateSuccessResponse (Guid requestId)
        {
            return new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                status: IpcResponseStatus.Ok,
                payload: JsonSerializer.SerializeToElement(new { ok = true }),
                errors: System.Array.Empty<IpcError>());
        }

        private static IpcResponse CreateErrorResponse (
            Guid requestId,
            UcliCode errorCode)
        {
            return new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                status: IpcResponseStatus.Error,
                payload: JsonSerializer.SerializeToElement(new { }),
                errors: new[]
                {
                    new IpcError(errorCode, "error", null),
                });
        }

    }
}
