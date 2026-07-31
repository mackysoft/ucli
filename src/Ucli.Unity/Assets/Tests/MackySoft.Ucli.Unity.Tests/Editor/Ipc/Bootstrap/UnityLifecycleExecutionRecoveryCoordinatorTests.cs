using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;
using static MackySoft.Ucli.Unity.Tests.LifecycleExecutionHandlerTestSupport;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityLifecycleExecutionRecoveryCoordinatorTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create(
                "lifecycle-recovery-coordinator");

        private static readonly UnityProjectIdentity Project = new(
            ProjectPathTestValues.RepositoryUnityProject,
            ProjectFingerprint,
            "2023.2.22f1");

        [Test]
        [Category("Size.Small")]
        public async Task RecoverAllAsync_WhenRegisteredUnityProcessExited_ReportsUnityExitedAndRetracksOpenExecution ()
        {
            using (var scope = TemporaryStorageScope.Create())
            {
                var executionStore =
                    scope.CreateExecutionStore(ProjectFingerprint);
                var definition = new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Refresh);
                var registeredProcess = new ProcessIdentity(42, 123);
                var endpointRegistrationGenerationId = Guid.NewGuid();
                var observedUtc = DateTimeOffset.UtcNow;
                _ = await RegisterAsync(
                    executionStore,
                    definition,
                    Guid.NewGuid(),
                    registeredProcess,
                    Guid.NewGuid(),
                    endpointRegistrationGenerationId,
                    new UnityEditorGenerationSnapshot(1, 1, 1, 1),
                    observedUtc.AddMinutes(1),
                    observedUtc);
                var handler = new RecordingRecoveryHandler(
                    LifecycleExecutionKind.Refresh);
                var delayObserved =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                using var coordinator =
                    new UnityLifecycleExecutionRecoveryCoordinator(
                        executionStore,
                        new UnityLifecycleExecutionHostContext(
                            new ProcessIdentity(99, 999),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            recoveryLease: null),
                        Project,
                        new[] { handler },
                        new ImmediateMainThreadRequestExecutor(),
                        NoOpDaemonLogger.Instance,
                        NoOpLifecycleExecutionTerminalObserver.Instance,
                        process =>
                        {
                            Assert.That(process, Is.EqualTo(registeredProcess));
                            return ProcessIdentityObservation
                                .ConfirmedExitedOrReplaced;
                        },
                        () => observedUtc,
                        (delay, cancellationToken) =>
                        {
                            delayObserved.TrySetResult(true);
                            return Task.Delay(
                                Timeout.InfiniteTimeSpan,
                                cancellationToken);
                        });

                await coordinator.RecoverAllAsync();
                var trackingTask = await Task.WhenAny(
                    delayObserved.Task,
                    Task.Delay(TimeSpan.FromSeconds(2)));

                Assert.That(handler.Requests, Has.Count.EqualTo(1));
                Assert.That(
                    handler.Requests[0].RejectionReason,
                    Is.EqualTo(
                        LifecycleExecutionTerminalReason.UnityExited));
                Assert.That(trackingTask, Is.SameAs(delayObserved.Task));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task RecoverAllAsync_WhenRejectedHostIsPastDeadline_UsesDeadlineWithoutAttributingCurrentProvider ()
        {
            using (var scope = TemporaryStorageScope.Create())
            {
                var executionStore =
                    scope.CreateExecutionStore(ProjectFingerprint);
                var definition = new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Refresh);
                var process = new ProcessIdentity(42, 123);
                var observedUtc = DateTimeOffset.UtcNow;
                _ = await RegisterAsync(
                    executionStore,
                    definition,
                    Guid.NewGuid(),
                    process,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    new UnityEditorGenerationSnapshot(1, 1, 1, 1),
                    observedUtc.AddMinutes(-1),
                    observedUtc.AddMinutes(-2));
                var handler = new DeferredTerminalRecoveryHandler(
                    executionStore,
                    throwOnFirstAttempt: false);
                var mainThreadExecutor =
                    new ImmediateMainThreadRequestExecutor();
                using var coordinator =
                    new UnityLifecycleExecutionRecoveryCoordinator(
                        executionStore,
                        new UnityLifecycleExecutionHostContext(
                            process,
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            recoveryLease: null),
                        Project,
                        new ILifecycleExecutionRecoveryHandler[] { handler },
                        mainThreadExecutor,
                        NoOpDaemonLogger.Instance,
                        NoOpLifecycleExecutionTerminalObserver.Instance,
                        _ => ProcessIdentityObservation.Same,
                        () => observedUtc,
                        static (_, cancellationToken) => Task.Delay(
                            Timeout.InfiniteTimeSpan,
                            cancellationToken));

                await coordinator.RecoverAllAsync();
                var publishedTask = await Task.WhenAny(
                    handler.TerminalPublished,
                    Task.Delay(TimeSpan.FromSeconds(2)));

                Assert.That(
                    publishedTask,
                    Is.SameAs(handler.TerminalPublished));
                Assert.That(handler.Requests, Has.Count.EqualTo(2));
                Assert.That(
                    handler.Requests,
                    Has.All.Matches<LifecycleExecutionRecoveryRequest>(
                        request =>
                            request.RejectionReason
                                == LifecycleExecutionTerminalReason
                                    .DeadlineExceeded
                            && !request
                                .CanAttributeCurrentProviderObservation));
                Assert.That(mainThreadExecutor.ExecuteCount, Is.EqualTo(2));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task RecoverAllAsync_WhenRegisteredProcessCannotBeObserved_KeepsExecutionRecoverableWithoutDispatch ()
        {
            using (var scope = TemporaryStorageScope.Create())
            {
                var executionStore =
                    scope.CreateExecutionStore(ProjectFingerprint);
                var definition = new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Refresh);
                var registeredProcess = new ProcessIdentity(42, 123);
                var endpointRegistrationGenerationId = Guid.NewGuid();
                var observedUtc = DateTimeOffset.UtcNow;
                var start = await RegisterAsync(
                    executionStore,
                    definition,
                    Guid.NewGuid(),
                    registeredProcess,
                    Guid.NewGuid(),
                    endpointRegistrationGenerationId,
                    new UnityEditorGenerationSnapshot(1, 1, 1, 1),
                    observedUtc.AddMinutes(1),
                    observedUtc);
                var handler = new RecordingRecoveryHandler(
                    LifecycleExecutionKind.Refresh);
                var delayObserved =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                using var coordinator =
                    new UnityLifecycleExecutionRecoveryCoordinator(
                        executionStore,
                        new UnityLifecycleExecutionHostContext(
                            new ProcessIdentity(99, 999),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            recoveryLease: null),
                        Project,
                        new[] { handler },
                        new ImmediateMainThreadRequestExecutor(),
                        NoOpDaemonLogger.Instance,
                        NoOpLifecycleExecutionTerminalObserver.Instance,
                        process =>
                        {
                            Assert.That(
                                process,
                                Is.EqualTo(registeredProcess));
                            return ProcessIdentityObservation.Unobservable;
                        },
                        () => observedUtc,
                        (delay, cancellationToken) =>
                        {
                            delayObserved.TrySetResult(true);
                            return Task.Delay(
                                Timeout.InfiniteTimeSpan,
                                cancellationToken);
                        });

                await coordinator.RecoverAllAsync();
                var trackingTask = await Task.WhenAny(
                    delayObserved.Task,
                    Task.Delay(TimeSpan.FromSeconds(2)));
                var stored = await executionStore.ReadAsync(
                    definition.Kind,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);

                Assert.That(trackingTask, Is.SameAs(delayObserved.Task));
                Assert.That(handler.Requests, Is.Empty);
                Assert.That(stored, Is.Not.Null);
                Assert.That(stored.IsTerminal, Is.False);
                Assert.That(
                    stored.CurrentReference,
                    Is.EqualTo(start.LifecycleExecutionRef));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task RecoverAllAsync_WhenOneExecutionRecoveryFails_LogsAndContinuesWithLaterExecutions ()
        {
            using (var scope = TemporaryStorageScope.Create())
            {
                var executionStore =
                    scope.CreateExecutionStore(ProjectFingerprint);
                var definition = new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Refresh);
                var observedUtc = DateTimeOffset.UtcNow;
                var process = new ProcessIdentity(42, 123);
                var editorInstanceId = Guid.NewGuid();
                var endpointRegistrationGenerationId = Guid.NewGuid();
                var executionIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
                for (var index = 0; index < executionIds.Length; index++)
                {
                    _ = await RegisterAsync(
                        executionStore,
                        definition,
                        executionIds[index],
                        process,
                        editorInstanceId,
                        endpointRegistrationGenerationId,
                        new UnityEditorGenerationSnapshot(1, 1, 1, 1),
                        observedUtc.AddMinutes(5),
                        observedUtc);
                }

                var handler = new ThrowingFirstRecoveryHandler(
                    LifecycleExecutionKind.Refresh);
                var mainThreadExecutor =
                    new ImmediateMainThreadRequestExecutor();
                var daemonLogger = new RecordingDaemonLogger();
                using var coordinator =
                    new UnityLifecycleExecutionRecoveryCoordinator(
                        executionStore,
                        new UnityLifecycleExecutionHostContext(
                            process,
                            editorInstanceId,
                            endpointRegistrationGenerationId,
                            recoveryLease: null),
                        Project,
                        new[] { handler },
                        mainThreadExecutor,
                        daemonLogger,
                        NoOpLifecycleExecutionTerminalObserver.Instance,
                        _ => ProcessIdentityObservation.Same,
                        () => observedUtc,
                        static (_, cancellationToken) => Task.Delay(
                            Timeout.InfiniteTimeSpan,
                            cancellationToken));

                await coordinator.RecoverAllAsync();

                Assert.That(handler.Requests, Has.Count.EqualTo(2));
                CollectionAssert.AreEquivalent(
                    executionIds,
                    new[]
                    {
                        handler.Requests[0].Start.LifecycleExecutionRef.Id,
                        handler.Requests[1].Start.LifecycleExecutionRef.Id,
                    });
                Assert.That(mainThreadExecutor.ExecuteCount, Is.EqualTo(2));
                Assert.That(daemonLogger.Exceptions, Has.Count.EqualTo(1));
                StringAssert.Contains(
                    handler.Requests[0].Start.LifecycleExecutionRef.Id.ToString("D"),
                    daemonLogger.Exceptions[0].Message);
                Assert.That(
                    daemonLogger.Exceptions[0].Exception,
                    Is.TypeOf<InvalidOperationException>());
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task RecoverAllAsync_WhenOneStoreRecordIsCorrupt_LogsAndRecoversOtherEntries ()
        {
            using (var scope = TemporaryStorageScope.Create())
            {
                var executionStore =
                    scope.CreateExecutionStore(ProjectFingerprint);
                var definition = new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Refresh);
                var observedUtc = DateTimeOffset.UtcNow;
                var process = new ProcessIdentity(42, 123);
                var editorInstanceId = Guid.NewGuid();
                var endpointRegistrationGenerationId = Guid.NewGuid();
                var corruptExecutionId = Guid.NewGuid();
                var recoverableExecutionId = Guid.NewGuid();
                var executionIds = new[]
                {
                    corruptExecutionId,
                    recoverableExecutionId,
                };
                for (var index = 0; index < executionIds.Length; index++)
                {
                    _ = await RegisterAsync(
                        executionStore,
                        definition,
                        executionIds[index],
                        process,
                        editorInstanceId,
                        endpointRegistrationGenerationId,
                        new UnityEditorGenerationSnapshot(1, 1, 1, 1),
                        observedUtc.AddMinutes(5),
                        observedUtc);
                }

                File.WriteAllText(
                    executionStore.Paths.ResolveRecordPath(
                        definition.Kind,
                        corruptExecutionId).Value,
                    "{");
                var handler = new RecordingRecoveryHandler(
                    LifecycleExecutionKind.Refresh);
                var mainThreadExecutor =
                    new ImmediateMainThreadRequestExecutor();
                var daemonLogger = new RecordingDaemonLogger();
                using var coordinator =
                    new UnityLifecycleExecutionRecoveryCoordinator(
                        executionStore,
                        new UnityLifecycleExecutionHostContext(
                            process,
                            editorInstanceId,
                            endpointRegistrationGenerationId,
                            recoveryLease: null),
                        Project,
                        new[] { handler },
                        mainThreadExecutor,
                        daemonLogger,
                        NoOpLifecycleExecutionTerminalObserver.Instance,
                        _ => ProcessIdentityObservation.Same,
                        () => observedUtc,
                        static (_, cancellationToken) => Task.Delay(
                            Timeout.InfiniteTimeSpan,
                            cancellationToken));

                await coordinator.RecoverAllAsync();

                Assert.That(handler.Requests, Has.Count.EqualTo(1));
                Assert.That(
                    handler.Requests[0].Start.LifecycleExecutionRef.Id,
                    Is.EqualTo(recoverableExecutionId));
                Assert.That(mainThreadExecutor.ExecuteCount, Is.EqualTo(1));
                Assert.That(daemonLogger.Exceptions, Has.Count.EqualTo(1));
                StringAssert.Contains(
                    corruptExecutionId.ToString("D"),
                    daemonLogger.Exceptions[0].Message);
                Assert.That(
                    daemonLogger.Exceptions[0].Exception,
                    Is.TypeOf<IOException>());
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task RecoverAllAsync_WhenEndpointGenerationAdvances_UsesCompileHandlerWithoutReissuingSideEffectAndPublishesTerminal ()
        {
            using (var scope = TemporaryStorageScope.Create())
            {
                var executionStore =
                    scope.CreateExecutionStore(ProjectFingerprint);
                var definition = new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Compile);
                var executionId = Guid.NewGuid();
                var observedUtc = DateTimeOffset.UtcNow;
                var process = new ProcessIdentity(42, 123);
                var editorInstanceId = Guid.NewGuid();
                var firstEndpointGenerationId = Guid.NewGuid();
                var successorEndpointGenerationId = Guid.NewGuid();
                var generation =
                    new UnityEditorGenerationSnapshot(1, 2, 3, 4);
                var start = await RegisterAsync(
                    executionStore,
                    definition,
                    executionId,
                    process,
                    editorInstanceId,
                    firstEndpointGenerationId,
                    generation,
                    observedUtc.AddMinutes(5),
                    observedUtc.AddMinutes(-1));
                var observation = CreateReadyCompileObservation(
                    generation,
                    observedUtc);
                var checkpointStore =
                    new FileCompileLifecycleExecutionCheckpointStore(
                        executionStore);
                var prepared = await checkpointStore.WritePreparedAsync(
                    executionId,
                    UnityLifecycleResponseFactory.Create(
                        Project,
                        "tests",
                        observation),
                    CreatePendingCompileResult(observation, observedUtc),
                    CancellationToken.None);
                var refreshingReference =
                    LifecycleExecutionReferenceFactory.CreateStateProjection(
                        start.LifecycleExecutionRef,
                        ExecutionLifecycle.Active,
                        LifecycleExecutionState.Refreshing);
                var sideEffectRight =
                    await executionStore.TryAcquireSideEffectRightAsync(
                        start.LifecycleExecutionRef,
                        refreshingReference,
                        firstEndpointGenerationId,
                        CancellationToken.None);
                Assert.That(
                    sideEffectRight.Outcome,
                    Is.EqualTo(
                        LifecycleExecutionSideEffectRightOutcome.Acquired));
                var admitted = await checkpointStore.MarkAdmittedAsync(
                    prepared,
                    CancellationToken.None);
                _ = await checkpointStore.MarkProviderReturnedAsync(
                    admitted,
                    CancellationToken.None);
                var provider =
                    new RecoveryCompileLifecycleExecutionProvider(
                        observation);
                var handler = new CompileLifecycleExecutionHandler(
                    provider,
                    NoOpDaemonLogger.Instance,
                    executionStore,
                    checkpointStore);
                var terminalObserver =
                    new RecordingLifecycleExecutionTerminalObserver();
                var recoveryLease = new DaemonLifecycleRecoveryLease(
                    firstEndpointGenerationId,
                    observedUtc.AddMinutes(1));
                using var coordinator =
                    new UnityLifecycleExecutionRecoveryCoordinator(
                        executionStore,
                        new UnityLifecycleExecutionHostContext(
                            process,
                            editorInstanceId,
                            successorEndpointGenerationId,
                            recoveryLease),
                        Project,
                        new ILifecycleExecutionRecoveryHandler[] { handler },
                        new ImmediateMainThreadRequestExecutor(),
                        NoOpDaemonLogger.Instance,
                        terminalObserver,
                        _ => ProcessIdentityObservation.Same,
                        () => observedUtc,
                        static (_, cancellationToken) => Task.Delay(
                            Timeout.InfiniteTimeSpan,
                            cancellationToken));

                await coordinator.RecoverAllAsync();

                var terminal = await executionStore.ReadAsync(
                    LifecycleExecutionKind.Compile,
                    executionId,
                    CancellationToken.None);
                Assert.That(terminal.IsTerminal, Is.True);
                Assert.That(
                    terminal.Start.Host.CurrentEndpointRegistrationGenerationId,
                    Is.EqualTo(successorEndpointGenerationId));
                Assert.That(provider.RefreshRequestCount, Is.EqualTo(0));
                Assert.That(provider.MutationCount, Is.EqualTo(0));
                Assert.That(
                    await terminalObserver.TerminalObserved,
                    Is.EqualTo((
                        LifecycleExecutionKind.Compile,
                        executionId)));
            }
        }

        [TestCase(LifecycleExecutionKind.Refresh)]
        [TestCase(LifecycleExecutionKind.Compile)]
        [TestCase(LifecycleExecutionKind.PlayEnter)]
        [TestCase(LifecycleExecutionKind.PlayExit)]
        [Category("Size.Small")]
        public async Task Track_WhenCalledTwice_WaitsOnceAndDispatchesOwningTypedDeadlineOnMainThread (
            LifecycleExecutionKind kind)
        {
            using (var scope = TemporaryStorageScope.Create())
            {
                var executionStore =
                    scope.CreateExecutionStore(ProjectFingerprint);
                var definition = new LifecycleExecutionDefinition(
                    kind);
                var executionId = Guid.NewGuid();
                var observedUtc = DateTimeOffset.UtcNow;
                var deadlineUtc = observedUtc.AddMinutes(1);
                var endpointRegistrationGenerationId = Guid.NewGuid();
                var start = await RegisterAsync(
                    executionStore,
                    definition,
                    executionId,
                    new ProcessIdentity(42, 123),
                    Guid.NewGuid(),
                    endpointRegistrationGenerationId,
                    new UnityEditorGenerationSnapshot(1, 1, 1, 1),
                    deadlineUtc,
                    observedUtc.AddMinutes(-1));
                var handler = new RecordingRecoveryHandler(
                    kind);
                var mainThreadExecutor =
                    new ImmediateMainThreadRequestExecutor();
                var delayObserved =
                    new TaskCompletionSource<TimeSpan>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseDelay =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                var delayInvocationCount = 0;
                using var coordinator =
                    new UnityLifecycleExecutionRecoveryCoordinator(
                        executionStore,
                        new UnityLifecycleExecutionHostContext(
                            new ProcessIdentity(42, 123),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            recoveryLease: null),
                        Project,
                        new[] { handler },
                        mainThreadExecutor,
                        NoOpDaemonLogger.Instance,
                        NoOpLifecycleExecutionTerminalObserver.Instance,
                        _ => ProcessIdentityObservation.Same,
                        () => observedUtc,
                        (delay, cancellationToken) =>
                        {
                            if (Interlocked.Increment(ref delayInvocationCount) > 1)
                            {
                                return Task.Delay(
                                    Timeout.InfiniteTimeSpan,
                                    cancellationToken);
                            }

                            Assert.That(
                                cancellationToken.IsCancellationRequested,
                                Is.False);
                            delayObserved.TrySetResult(delay);
                            return releaseDelay.Task;
                        });

                coordinator.Track(
                    kind,
                    executionId);
                coordinator.Track(
                    kind,
                    executionId);

                var delayTask = await Task.WhenAny(
                    delayObserved.Task,
                    Task.Delay(TimeSpan.FromSeconds(2)));
                Assert.That(delayTask, Is.SameAs(delayObserved.Task));
                Assert.That(
                    await delayObserved.Task,
                    Is.EqualTo(TimeSpan.FromMinutes(1)));
                observedUtc = deadlineUtc;
                releaseDelay.TrySetResult(true);
                var recoveryTask = await Task.WhenAny(
                    handler.RequestObserved,
                    Task.Delay(TimeSpan.FromSeconds(2)));

                Assert.That(recoveryTask, Is.SameAs(handler.RequestObserved));
                Assert.That(handler.Requests, Has.Count.EqualTo(1));
                Assert.That(
                    handler.Requests[0].Start,
                    Is.EqualTo(start));
                Assert.That(
                    handler.Requests[0].RejectionReason,
                    Is.EqualTo(
                        LifecycleExecutionTerminalReason.DeadlineExceeded));
                Assert.That(mainThreadExecutor.ExecuteCount, Is.EqualTo(1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("Size.Small")]
        public async Task TrackUntilTerminalAsync_WhenDeadlineRecoveryRemainsOpen_RetriesUntilTerminal (
            bool throwOnFirstAttempt)
        {
            using (var scope = TemporaryStorageScope.Create())
            {
                var executionStore =
                    scope.CreateExecutionStore(ProjectFingerprint);
                var definition = new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Refresh);
                var executionId = Guid.NewGuid();
                var observedUtc = DateTimeOffset.UtcNow;
                var endpointRegistrationGenerationId = Guid.NewGuid();
                var editorInstanceId = Guid.NewGuid();
                _ = await RegisterAsync(
                    executionStore,
                    definition,
                    executionId,
                    new ProcessIdentity(42, 123),
                    editorInstanceId,
                    endpointRegistrationGenerationId,
                    new UnityEditorGenerationSnapshot(1, 1, 1, 1),
                    observedUtc,
                    observedUtc.AddMinutes(-1));
                var handler = new DeferredTerminalRecoveryHandler(
                    executionStore,
                    throwOnFirstAttempt);
                var mainThreadExecutor =
                    new ImmediateMainThreadRequestExecutor();
                var daemonLogger = new RecordingDaemonLogger();
                var terminalObserver =
                    new RecordingLifecycleExecutionTerminalObserver();
                var retryDelayObserved =
                    new TaskCompletionSource<TimeSpan>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseRetry =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                var delayInvocationCount = 0;
                using var coordinator =
                    new UnityLifecycleExecutionRecoveryCoordinator(
                        executionStore,
                        new UnityLifecycleExecutionHostContext(
                            new ProcessIdentity(42, 123),
                            editorInstanceId,
                            endpointRegistrationGenerationId,
                            recoveryLease: null),
                        Project,
                        new ILifecycleExecutionRecoveryHandler[] { handler },
                        mainThreadExecutor,
                        daemonLogger,
                        terminalObserver,
                        _ => ProcessIdentityObservation.Same,
                        () => observedUtc,
                        (delay, cancellationToken) =>
                        {
                            if (Interlocked.Increment(ref delayInvocationCount) == 1)
                            {
                                retryDelayObserved.TrySetResult(delay);
                                return releaseRetry.Task;
                            }

                            return Task.Delay(
                                Timeout.InfiniteTimeSpan,
                                cancellationToken);
                        });

                coordinator.Track(definition.Kind, executionId);
                var retryObservedTask = await Task.WhenAny(
                    retryDelayObserved.Task,
                    Task.Delay(TimeSpan.FromSeconds(2)));
                Assert.That(
                    retryObservedTask,
                    Is.SameAs(retryDelayObserved.Task));
                Assert.That(
                    await retryDelayObserved.Task,
                    Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(handler.Requests, Has.Count.EqualTo(1));
                Assert.That(
                    (await executionStore.ReadAsync(
                        definition.Kind,
                        executionId,
                        CancellationToken.None)).IsTerminal,
                    Is.False);

                releaseRetry.TrySetResult(true);
                var publishedTask = await Task.WhenAny(
                    handler.TerminalPublished,
                    Task.Delay(TimeSpan.FromSeconds(2)));

                Assert.That(
                    publishedTask,
                    Is.SameAs(handler.TerminalPublished));
                var terminalObservedTask = await Task.WhenAny(
                    terminalObserver.TerminalObserved,
                    Task.Delay(TimeSpan.FromSeconds(2)));
                Assert.That(
                    terminalObservedTask,
                    Is.SameAs(terminalObserver.TerminalObserved));
                Assert.That(
                    await terminalObserver.TerminalObserved,
                    Is.EqualTo((
                        LifecycleExecutionKind.Refresh,
                        executionId)));
                Assert.That(handler.Requests, Has.Count.EqualTo(2));
                Assert.That(mainThreadExecutor.ExecuteCount, Is.EqualTo(2));
                Assert.That(
                    daemonLogger.Exceptions,
                    Has.Count.EqualTo(throwOnFirstAttempt ? 1 : 0));
                Assert.That(
                    (await executionStore.ReadAsync(
                        definition.Kind,
                        executionId,
                        CancellationToken.None)).IsTerminal,
                    Is.True);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        [Category("Size.Small")]
        public async Task TrackUntilTerminalAsync_WhenExecutionIsTerminal_NotifiesOnlyOwningHostWithoutDelayOrDispatch (
            bool ownedByCurrentHost)
        {
            using (var scope = TemporaryStorageScope.Create())
            {
                var executionStore =
                    scope.CreateExecutionStore(ProjectFingerprint);
                var definition = new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Refresh);
                var executionId = Guid.NewGuid();
                var observedUtc = DateTimeOffset.UtcNow;
                var endpointRegistrationGenerationId = Guid.NewGuid();
                var editorInstanceId = Guid.NewGuid();
                var start = await RegisterAsync(
                    executionStore,
                    definition,
                    executionId,
                    new ProcessIdentity(42, 123),
                    editorInstanceId,
                    endpointRegistrationGenerationId,
                    new UnityEditorGenerationSnapshot(1, 1, 1, 1),
                    observedUtc.AddMinutes(1),
                    observedUtc.AddMinutes(-1));
                var publication = await executionStore.PublishTerminalAsync(
                    CreateDeadlineTerminalRecord(start),
                    CancellationToken.None);
                Assert.That(publication.IsSuccess, Is.True);
                var handler = new RecordingRecoveryHandler(
                    LifecycleExecutionKind.Refresh);
                var mainThreadExecutor =
                    new ImmediateMainThreadRequestExecutor();
                var delayCount = 0;
                var terminalObserver =
                    new RecordingLifecycleExecutionTerminalObserver();
                using var coordinator =
                    new UnityLifecycleExecutionRecoveryCoordinator(
                        executionStore,
                        new UnityLifecycleExecutionHostContext(
                            new ProcessIdentity(42, 123),
                            ownedByCurrentHost
                                ? editorInstanceId
                                : Guid.NewGuid(),
                            Guid.NewGuid(),
                            recoveryLease: null),
                        Project,
                        new[] { handler },
                        mainThreadExecutor,
                        NoOpDaemonLogger.Instance,
                        terminalObserver,
                        _ => ProcessIdentityObservation.Same,
                        () => observedUtc,
                        (delay, cancellationToken) =>
                        {
                            Interlocked.Increment(ref delayCount);
                            return Task.CompletedTask;
                        });

                await coordinator.TrackUntilTerminalAsync(
                    LifecycleExecutionKind.Refresh,
                    executionId);

                Assert.That(delayCount, Is.EqualTo(0));
                Assert.That(handler.Requests, Is.Empty);
                Assert.That(mainThreadExecutor.ExecuteCount, Is.EqualTo(0));
                if (ownedByCurrentHost)
                {
                    Assert.That(
                        await terminalObserver.TerminalObserved,
                        Is.EqualTo((
                            LifecycleExecutionKind.Refresh,
                            executionId)));
                }
                else
                {
                    Assert.That(
                        terminalObserver.TerminalObserved.IsCompleted,
                        Is.False);
                }
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Dispose_WhileDeadlineIsPending_PreventsTypedRecoveryDispatch ()
        {
            using (var scope = TemporaryStorageScope.Create())
            {
                var executionStore =
                    scope.CreateExecutionStore(ProjectFingerprint);
                var definition = new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Refresh);
                var executionId = Guid.NewGuid();
                var observedUtc = DateTimeOffset.UtcNow;
                var endpointRegistrationGenerationId = Guid.NewGuid();
                _ = await RegisterAsync(
                    executionStore,
                    definition,
                    executionId,
                    new ProcessIdentity(42, 123),
                    Guid.NewGuid(),
                    endpointRegistrationGenerationId,
                    new UnityEditorGenerationSnapshot(1, 1, 1, 1),
                    observedUtc.AddMinutes(1),
                    observedUtc.AddMinutes(-1));
                var handler = new RecordingRecoveryHandler(
                    LifecycleExecutionKind.Refresh);
                var mainThreadExecutor =
                    new ImmediateMainThreadRequestExecutor();
                var delayObserved =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                var cancellationObserved =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                using var coordinator =
                    new UnityLifecycleExecutionRecoveryCoordinator(
                        executionStore,
                        new UnityLifecycleExecutionHostContext(
                            new ProcessIdentity(42, 123),
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            recoveryLease: null),
                        Project,
                        new[] { handler },
                        mainThreadExecutor,
                        NoOpDaemonLogger.Instance,
                        NoOpLifecycleExecutionTerminalObserver.Instance,
                        _ => ProcessIdentityObservation.Same,
                        () => observedUtc,
                        (delay, cancellationToken) =>
                        {
                            delayObserved.TrySetResult(true);
                            _ = cancellationToken.Register(
                                () => cancellationObserved.TrySetResult(true));
                            return Task.Delay(
                                Timeout.InfiniteTimeSpan,
                                cancellationToken);
                        });
                coordinator.Track(
                    LifecycleExecutionKind.Refresh,
                    executionId);
                var enteredTask = await Task.WhenAny(
                    delayObserved.Task,
                    Task.Delay(TimeSpan.FromSeconds(2)));
                Assert.That(enteredTask, Is.SameAs(delayObserved.Task));

                coordinator.Dispose();

                var canceledTask = await Task.WhenAny(
                    cancellationObserved.Task,
                    Task.Delay(TimeSpan.FromSeconds(2)));
                Assert.That(
                    canceledTask,
                    Is.SameAs(cancellationObserved.Task));
                Assert.That(handler.Requests, Is.Empty);
                Assert.That(mainThreadExecutor.ExecuteCount, Is.EqualTo(0));
            }
        }

        private static RefreshLifecycleExecutionTerminalRecord
            CreateDeadlineTerminalRecord (
                LifecycleExecutionStartBinding start)
        {
            return new RefreshLifecycleExecutionTerminalRecord(
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
                Array.Empty<ArtifactRef>());
        }

        private static async Task<LifecycleExecutionStartBinding> RegisterAsync (
            FileLifecycleExecutionStore executionStore,
            LifecycleExecutionDefinition definition,
            Guid executionId,
            ProcessIdentity process,
            Guid editorInstanceId,
            Guid firstEndpointRegistrationGenerationId,
            UnityEditorGenerationSnapshot startedGeneration,
            DateTimeOffset deadlineUtc,
            DateTimeOffset startedAtUtc,
            Guid? currentEndpointRegistrationGenerationId = null)
        {
            var result = await executionStore.StartAsync(
                definition,
                executionId,
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                Project,
                new LifecycleExecutionHostRegistration(
                    process,
                    editorInstanceId,
                    firstEndpointRegistrationGenerationId,
                    currentEndpointRegistrationGenerationId
                        ?? firstEndpointRegistrationGenerationId),
                startedGeneration,
                deadlineUtc,
                startedAtUtc,
                CancellationToken.None);
            Assert.That(result.IsSuccess, Is.True);
            return result.Binding;
        }

        private static UnityEditorRuntimeObservation CreateReadyCompileObservation (
            UnityEditorGenerationSnapshot generation,
            DateTimeOffset observedUtc)
        {
            return new UnityEditorRuntimeObservation(
                new UnityEditorStateSnapshot(
                    UnityEditorMode.Batchmode,
                    UnityEditorLifecycleState.Ready,
                    UnityEditorCompileState.Ready,
                    generation,
                    new UnityEditorPlayModeSnapshot(
                        UnityEditorPlayModeState.Stopped,
                        UnityEditorPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                observedUtc,
                primaryDiagnostic: null);
        }

        private static CompileLifecycleResult CreatePendingCompileResult (
            UnityEditorRuntimeObservation before,
            DateTimeOffset startedAtUtc)
        {
            return new CompileLifecycleResult(
                new CompileLifecycleResult.RefreshEvidence(
                    CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
                    Requested: true,
                    startedAtUtc,
                    CompletedAtUtc: null,
                    Completed: false),
                new CompileLifecycleResult.ScriptCompilationEvidence(
                    Started: false,
                    Completed: false,
                    before.State.Generations.CompileGeneration,
                    before.State.Generations.CompileGeneration,
                    new CompileLifecycleResult.DiagnosticsEvidence(
                        ErrorCount: 0,
                        WarningCount: 0,
                        PrimaryDiagnostic: null)),
                new CompileLifecycleResult.DomainReloadEvidence(
                    ReloadRequired: false,
                    ReloadObserved: false,
                    before.State.Generations.DomainReloadGeneration,
                    before.State.Generations.DomainReloadGeneration,
                    Settled: false),
                new CompileLifecycleResult.LifecycleEvidence(
                    "tests",
                    Project.UnityVersion,
                    before.State,
                    before.ObservedAtUtc,
                    before.ActionRequired,
                    before.PrimaryDiagnostic));
        }

        private sealed class RecoveryCompileLifecycleExecutionProvider :
            ICompileLifecycleExecutionProvider
        {
            private readonly UnityEditorRuntimeObservation observation;

            public RecoveryCompileLifecycleExecutionProvider (
                UnityEditorRuntimeObservation observation)
            {
                this.observation = observation;
            }

            public int RefreshRequestCount { get; private set; }

            public int MutationCount { get; private set; }

            public UnityEditorRuntimeObservation CaptureObservation ()
            {
                return observation;
            }

            public UnityEditorObservation CreateLifecycleObservation (
                UnityEditorRuntimeObservation current)
            {
                return UnityLifecycleResponseFactory.Create(
                    Project,
                    "tests",
                    current);
            }

            public CompileLifecycleResult.LifecycleEvidence
                CreateLifecycleEvidence (
                    UnityEditorRuntimeObservation current)
            {
                return new CompileLifecycleResult.LifecycleEvidence(
                    "tests",
                    Project.UnityVersion,
                    current.State,
                    current.ObservedAtUtc,
                    current.ActionRequired,
                    current.PrimaryDiagnostic);
            }

            public IUnityMutationActivity BeginMutation ()
            {
                MutationCount++;
                return NoOpMutationActivity.Instance;
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
                return NoOpDisposable.Instance;
            }
        }

        private sealed class NoOpMutationActivity : IUnityMutationActivity
        {
            public static NoOpMutationActivity Instance { get; } =
                new NoOpMutationActivity();

            private NoOpMutationActivity ()
            {
            }

            public void Complete ()
            {
            }
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public static NoOpDisposable Instance { get; } =
                new NoOpDisposable();

            private NoOpDisposable ()
            {
            }

            public void Dispose ()
            {
            }
        }

        private sealed class RecordingRecoveryHandler :
            ILifecycleExecutionRecoveryHandler
        {
            public RecordingRecoveryHandler (LifecycleExecutionKind kind)
            {
                Kind = kind;
            }

            public LifecycleExecutionKind Kind { get; }

            public List<LifecycleExecutionRecoveryRequest> Requests { get; } =
                new List<LifecycleExecutionRecoveryRequest>();

            public Task<LifecycleExecutionRecoveryRequest> RequestObserved =>
                requestObserved.Task;

            private readonly TaskCompletionSource<LifecycleExecutionRecoveryRequest>
                requestObserved =
                    new TaskCompletionSource<LifecycleExecutionRecoveryRequest>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

            public ValueTask RecoverAsync (
                LifecycleExecutionRecoveryRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                requestObserved.TrySetResult(request);
                return default;
            }
        }

        private sealed class RecordingLifecycleExecutionTerminalObserver :
            ILifecycleExecutionTerminalObserver
        {
            private readonly TaskCompletionSource<(
                LifecycleExecutionKind Kind,
                Guid ExecutionId)> terminalObserved =
                    new TaskCompletionSource<(
                        LifecycleExecutionKind Kind,
                        Guid ExecutionId)>(
                            TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<(LifecycleExecutionKind Kind, Guid ExecutionId)>
                TerminalObserved => terminalObserved.Task;

            public void OnTerminal (
                LifecycleExecutionKind kind,
                Guid executionId)
            {
                terminalObserved.TrySetResult((kind, executionId));
            }
        }

        private sealed class ThrowingFirstRecoveryHandler :
            ILifecycleExecutionRecoveryHandler
        {
            private int invocationCount;

            public ThrowingFirstRecoveryHandler (LifecycleExecutionKind kind)
            {
                Kind = kind;
            }

            public LifecycleExecutionKind Kind { get; }

            public List<LifecycleExecutionRecoveryRequest> Requests { get; } =
                new List<LifecycleExecutionRecoveryRequest>();

            public ValueTask RecoverAsync (
                LifecycleExecutionRecoveryRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                if (Interlocked.Increment(ref invocationCount) == 1)
                {
                    throw new InvalidOperationException(
                        "Simulated recovery failure.");
                }

                return default;
            }
        }

        private sealed class DeferredTerminalRecoveryHandler :
            ILifecycleExecutionRecoveryHandler
        {
            private readonly FileLifecycleExecutionStore executionStore;
            private readonly bool throwOnFirstAttempt;
            private readonly TaskCompletionSource<bool> terminalPublished =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            private int invocationCount;

            public DeferredTerminalRecoveryHandler (
                FileLifecycleExecutionStore executionStore,
                bool throwOnFirstAttempt)
            {
                this.executionStore = executionStore;
                this.throwOnFirstAttempt = throwOnFirstAttempt;
            }

            public LifecycleExecutionKind Kind =>
                LifecycleExecutionKind.Refresh;

            public List<LifecycleExecutionRecoveryRequest> Requests { get; } =
                new List<LifecycleExecutionRecoveryRequest>();

            public Task TerminalPublished => terminalPublished.Task;

            public async ValueTask RecoverAsync (
                LifecycleExecutionRecoveryRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                if (Interlocked.Increment(ref invocationCount) == 1)
                {
                    if (throwOnFirstAttempt)
                    {
                        throw new IOException(
                            "Simulated transient terminal publication failure.");
                    }

                    return;
                }

                var publication = await executionStore.PublishTerminalAsync(
                    CreateDeadlineTerminalRecord(request.Start),
                    cancellationToken);
                if (!publication.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"Could not publish terminal record: {publication.Outcome}.");
                }

                terminalPublished.TrySetResult(true);
            }
        }

        private sealed class RecordingDaemonLogger : IDaemonLogger
        {
            public List<ExceptionLog> Exceptions { get; } =
                new List<ExceptionLog>();

            public void Info (
                string category,
                string message,
                string raw = null)
            {
            }

            public void Warning (
                string category,
                string message,
                string raw = null)
            {
            }

            public void Error (
                string category,
                string message,
                string raw = null)
            {
            }

            public void Exception (
                string category,
                string message,
                Exception exception)
            {
                Exceptions.Add(new ExceptionLog(message, exception));
            }
        }

        private sealed class ExceptionLog
        {
            public ExceptionLog (
                string message,
                Exception exception)
            {
                Message = message;
                Exception = exception;
            }

            public string Message { get; }

            public Exception Exception { get; }
        }

        private sealed class ImmediateMainThreadRequestExecutor :
            IUnityMainThreadRequestExecutor
        {
            private int executeCount;

            public int ExecuteCount => Volatile.Read(ref executeCount);

            public Task<T> ExecuteAsync<T> (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref executeCount);
                return workItem();
            }
        }
    }
}
