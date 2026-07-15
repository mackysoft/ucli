using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGuiBootstrapTests
    {
        private static readonly Guid EditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid LifecycleSidecarGenerationId =
            Guid.Parse("22222222-2222-2222-2222-222222222222");

        [Test]
        [Category("Size.Small")]
        public void StartingGeneration_WhenLifecycleSidecarGenerationIdIsEmpty_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() => new UnityGuiBootstrap.StartingGuiBootstrapState(
                CancellationToken.None,
                EditorInstanceId,
                Guid.Empty,
                NoOpDaemonLogger.Instance));

            Assert.That(exception.ParamName, Is.EqualTo("lifecycleSidecarGenerationId"));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartServerAndPublishSession_WhenServerStarts_PublishesSessionOnlyAfterListenSucceeds () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            UnityGuiSessionPersistence.PreparedSession preparedSession = null;
            try
            {
                var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-bootstrap-publication-order");
                preparedSession = await UnityGuiSessionPersistence.PrepareAsync(
                    storageRoot,
                    ProjectFingerprintTestFactory.Create("fingerprint-publication-order"),
                    endpoint,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    EditorInstanceId,
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession,
                    CancellationToken.None);
                var server = new SpyUnityIpcServer(onStart: _ =>
                    Assert.That(File.Exists(preparedSession.SessionPath), Is.False));

                var startResult = await UnityGuiBootstrap.StartServerAndPublishSessionAsync(
                    server,
                    endpoint,
                    CancellationToken.None,
                    static () => { },
                    () => UnityGuiSessionPersistence.PublishAsync(
                        preparedSession,
                        CancellationToken.None));
                using var publicationFence = startResult.PublicationFence;
                var ownershipCommitted = false;
                Assert.That(
                    publicationFence.TryCommitActiveOwnership(() => ownershipCommitted = true),
                    Is.True);

                Assert.That(server.StartCallCount, Is.EqualTo(1));
                Assert.That(ownershipCommitted, Is.True);
                Assert.That(File.Exists(startResult.Registration.SessionPath), Is.True);
            }
            finally
            {
                preparedSession?.Dispose();
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartServerAndPublishSession_WhenServerStartFails_DoesNotPublishSession () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            UnityGuiSessionPersistence.PreparedSession preparedSession = null;
            try
            {
                var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-bootstrap-failed-listen");
                preparedSession = await UnityGuiSessionPersistence.PrepareAsync(
                    storageRoot,
                    ProjectFingerprintTestFactory.Create("fingerprint-failed-listen"),
                    endpoint,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    EditorInstanceId,
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession,
                    CancellationToken.None);
                var server = new SpyUnityIpcServer(onStart: _ =>
                    throw new InvalidOperationException("listen failed"));
                InvalidOperationException observedException = null;

                try
                {
                    await UnityGuiBootstrap.StartServerAndPublishSessionAsync(
                        server,
                        endpoint,
                        CancellationToken.None,
                        static () => { },
                        () => UnityGuiSessionPersistence.PublishAsync(
                            preparedSession,
                            CancellationToken.None));
                }
                catch (InvalidOperationException exception)
                {
                    observedException = exception;
                }

                Assert.That(observedException, Is.Not.Null);
                Assert.That(server.StartCallCount, Is.EqualTo(1));
                Assert.That(File.Exists(preparedSession.SessionPath), Is.False);
            }
            finally
            {
                preparedSession?.Dispose();
                DeleteDirectory(storageRoot);
            }
        });

        [TestCase((int)UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession, false, false, false)]
        [TestCase((int)UnityGuiSessionReplacementScope.AnyCurrentProcessSession, false, false, true)]
        [TestCase((int)UnityGuiSessionReplacementScope.AnyCurrentProcessSession, true, false, false)]
        [TestCase((int)UnityGuiSessionReplacementScope.AnyCurrentProcessSession, false, true, false)]
        [Category("Size.Small")]
        public void CanReplaceActiveSession_RequiresExplicitScopeAndIdleMutationLane (
            int replacementScopeValue,
            bool isMutationBusy,
            bool hasUnfinishedWork,
            bool expected)
        {
            Assert.That(
                UnityGuiBootstrap.CanReplaceActiveSession(
                    (UnityGuiSessionReplacementScope)replacementScopeValue,
                    isMutationBusy,
                    hasUnfinishedWork),
                Is.EqualTo(expected));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CleanupFailedStart_WhenSessionWasWritten_StopsServerAndDeletesSession () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                var registration = await PrepareAndPublishSessionAsync(storageRoot);
                var server = new SpyUnityIpcServer();
                var logCapture = new SpyDisposable();
                var serviceProvider = new SpyServiceProvider();

                await UnityGuiBootstrap.CleanupFailedStartAsync(
                    registration,
                    server,
                    null,
                    logCapture,
                    serviceProvider,
                    NoOpDaemonLogger.Instance);

                Assert.That(server.StopCallCount, Is.EqualTo(1));
                Assert.That(logCapture.DisposeCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
                Assert.That(File.Exists(registration.SessionPath), Is.False);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CleanupFailedStart_WhenServiceProviderDisposeFails_StillDeletesSession () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                var registration = await PrepareAndPublishSessionAsync(storageRoot);

                await UnityGuiBootstrap.CleanupFailedStartAsync(
                    registration,
                    server: null,
                    lifecycleSidecarWriter: null,
                    unityLogCaptureService: null,
                    serviceProvider: new SpyServiceProvider(throwOnDispose: true),
                    daemonLogger: NoOpDaemonLogger.Instance);

                Assert.That(File.Exists(registration.SessionPath), Is.False);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CleanupFailedStart_WhenServerStopFails_RetainsDependenciesUntilEditorLifecycleRelease () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                var registration = await PrepareAndPublishSessionAsync(storageRoot);
                var server = new SpyUnityIpcServer(throwOnStop: true);
                var logCapture = new SpyDisposable();
                var serviceProvider = new SpyServiceProvider();

                var stoppedSafely = await UnityGuiBootstrap.CleanupFailedStartAsync(
                    registration,
                    server,
                    null,
                    logCapture,
                    serviceProvider,
                    NoOpDaemonLogger.Instance);

                Assert.That(stoppedSafely, Is.False);
                Assert.That(server.StopCallCount, Is.EqualTo(1));
                Assert.That(logCapture.DisposeCallCount, Is.Zero);
                Assert.That(serviceProvider.DisposeCallCount, Is.Zero);
                Assert.That(File.Exists(registration.SessionPath), Is.False);

                UnityGuiBootstrap.ReleaseResourcesForEditorLifecycleEvent(
                    null,
                    server,
                    logCapture,
                    serviceProvider,
                    NoOpDaemonLogger.Instance,
                    deleteSession: false);

                Assert.That(server.ReleaseCallCount, Is.EqualTo(1));
                Assert.That(logCapture.DisposeCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CleanupFailedStart_WhenMutationHasNotRetired_ReturnsWithoutDisposingProviderAndReleasesItAfterRetirement () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            var mutationLane = new DeferredRetirementMutationLaneControl();
            try
            {
                var registration = await PrepareAndPublishSessionAsync(storageRoot);
                var server = new SpyUnityIpcServer();
                var logCapture = new SpyDisposable();
                var serviceProvider = new SpyServiceProvider(mutationLaneControl: mutationLane);

                var cleanupTask = UnityGuiBootstrap.CleanupFailedStartAsync(
                    registration,
                    server,
                    null,
                    logCapture,
                    serviceProvider,
                    NoOpDaemonLogger.Instance);

                var stoppedSafely = await TestAwaiter.WaitAsync(
                    cleanupTask.AsUniTask(),
                    "Bounded failed-start mutation retirement",
                    TimeSpan.FromSeconds(2));

                Assert.That(stoppedSafely, Is.False);
                Assert.That(server.StopCallCount, Is.EqualTo(1));
                Assert.That(File.Exists(registration.SessionPath), Is.False);
                Assert.That(logCapture.DisposeCallCount, Is.Zero);
                Assert.That(serviceProvider.DisposeCallCount, Is.Zero);

                mutationLane.CompleteRetirement();
                await TestAwaiter.WaitAsync(
                    UniTask.WaitUntil(() => serviceProvider.DisposeCallCount == 1),
                    "Deferred failed-start resource release",
                    TimeSpan.FromSeconds(5));
                Assert.That(logCapture.DisposeCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
            }
            finally
            {
                mutationLane.CompleteRetirement();
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StopAndRebootstrap_WhenOldSidecarWriteIgnoresCancellation_KeepsSuccessorFencedUntilWriterStops () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            var sharedState = new SharedLifecycleSidecarState();
            var oldPersistence = new ControllableLifecycleSidecarPersistence(
                sharedState,
                delaySecondWrite: true,
                failDeletion: false);
            var oldWriter = new UnityLifecycleSidecarWriter(oldPersistence);
            var oldInitialObservation = CreateReadyObservation(
                new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
                generation: 1);
            var delayedOldObservation = CreateReadyObservation(
                new DateTimeOffset(2026, 7, 14, 0, 0, 1, TimeSpan.Zero),
                generation: 1);
            await oldWriter.InitializeAsync(
                oldInitialObservation,
                oldInitialObservation.ObservedAtUtc,
                CancellationToken.None);
            Assert.That(
                oldWriter.TryEnqueue(
                    delayedOldObservation,
                    delayedOldObservation.ObservedAtUtc,
                    out _),
                Is.True);
            await TestAwaiter.WaitAsync(
                oldPersistence.DelayedWriteStarted,
                "Old generation sidecar write start",
                TimeSpan.FromSeconds(5));
            var mutationLane = new ImmediateUnityMutationLaneControl();
            var serviceProvider = new SpyServiceProvider(mutationLaneControl: mutationLane);
            var availabilityObservationSource = new StaticAvailabilityObservationSource(oldInitialObservation);
            var mutationExecutionStartSource = new StubMutationRequestExecutionStartSource();
            var sidecarObserver = new UnityMutationLifecycleSidecarObserver(
                mutationExecutionStartSource,
                availabilityObservationSource,
                oldWriter,
                NoOpDaemonLogger.Instance);
            var logCaptureService = CreateUnityLogCaptureService();
            var activeStateField = GetGuiBootstrapStateField("activeState");
            var stoppingStateField = GetGuiBootstrapStateField("stoppingState");
            Assert.That(activeStateField.GetValue(null), Is.Null);
            Assert.That(stoppingStateField.GetValue(null), Is.Null);

            try
            {
                var registration = await PrepareAndPublishSessionAsync(storageRoot);
                var activeState = CreateActiveGuiBootstrapState(
                    registration,
                    new SpyUnityIpcServer(),
                    logCaptureService,
                    serviceProvider,
                    availabilityObservationSource,
                    mutationLane,
                    sidecarObserver,
                    oldWriter);
                activeStateField.SetValue(null, activeState);

                await TestAwaiter.WaitAsync(
                    UnityGuiBootstrap.StopAsync(CancellationToken.None).AsUniTask(),
                    "Bounded old sidecar writer stop",
                    TimeSpan.FromSeconds(5));
                var oldStopTask = oldWriter.StopAsync(CancellationToken.None);
                var rebootstrapResult = await UnityGuiBootstrap.StartAsync(
                    bootstrapArguments: null,
                    UnityGuiSessionReplacementScope.AnyCurrentProcessSession,
                    CancellationToken.None);

                Assert.That(oldStopTask.IsCompleted, Is.False);
                Assert.That(stoppingStateField.GetValue(null), Is.SameAs(activeState));
                Assert.That(rebootstrapResult.IsSuccess, Is.False);
                Assert.That(rebootstrapResult.ErrorMessage, Does.Contain("still retiring"));
                Assert.That(sharedState.LatestObservation, Is.SameAs(oldInitialObservation));

                oldPersistence.ReleaseDelayedWrite();
                await TestAwaiter.WaitAsync(
                    oldStopTask,
                    "Old sidecar writer retirement",
                    TimeSpan.FromSeconds(5));
                await TestAwaiter.WaitAsync(
                    UniTask.WaitUntil(() => stoppingStateField.GetValue(null) == null),
                    "Old GUI generation fence release",
                    TimeSpan.FromSeconds(5));

                var successorEndpoint = new IpcEndpoint(
                    IpcTransportKind.NamedPipe,
                    "ucli-gui-bootstrap-fenced-successor");
                using var successorSession = await UnityGuiSessionPersistence.PrepareAsync(
                    storageRoot,
                    ProjectFingerprintTestFactory.Create("fingerprint-fenced-successor"),
                    successorEndpoint,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    EditorInstanceId,
                    UnityGuiSessionReplacementScope.AnyCurrentProcessSession,
                    CancellationToken.None);
                var successorServer = new SpyUnityIpcServer();
                var successorStart = await UnityGuiBootstrap.StartServerAndPublishSessionAsync(
                    successorServer,
                    successorEndpoint,
                    CancellationToken.None,
                    static () => { },
                    () => UnityGuiSessionPersistence.PublishAsync(
                        successorSession,
                        CancellationToken.None));
                using var successorPublicationFence = successorStart.PublicationFence;
                var successorOwnershipCommitted = false;
                var successorStarted = successorPublicationFence.TryCommitActiveOwnership(
                    () => successorOwnershipCommitted = true);

                Assert.That(successorStarted, Is.True);
                Assert.That(successorOwnershipCommitted, Is.True);
                Assert.That(successorServer.StartCallCount, Is.EqualTo(1));
                Assert.That(File.Exists(successorStart.Registration.SessionPath), Is.True);
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
            }
            finally
            {
                oldPersistence.ReleaseDelayedWrite();
                await oldWriter.StopAsync(CancellationToken.None);
                await UnityGuiBootstrap.StopAsync(CancellationToken.None);
                activeStateField.SetValue(null, null);
                stoppingStateField.SetValue(null, null);
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ScheduleDomainReloadRecovery_PersistsLeaseForCurrentSessionGeneration () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            UnityGuiSessionRegistration registration = null;
            var sharedState = new SharedLifecycleSidecarState();
            var persistence = new ControllableLifecycleSidecarPersistence(
                sharedState,
                delaySecondWrite: false,
                failDeletion: false);
            var writer = new UnityLifecycleSidecarWriter(persistence);
            var readyObservation = CreateReadyObservation(
                new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
                generation: 1);
            var sidecarObserver = new UnityMutationLifecycleSidecarObserver(
                new StubMutationRequestExecutionStartSource(),
                new StaticAvailabilityObservationSource(readyObservation),
                writer,
                NoOpDaemonLogger.Instance);

            try
            {
                registration = await PrepareAndPublishSessionAsync(storageRoot);
                await writer.InitializeAsync(
                    readyObservation,
                    readyObservation.ObservedAtUtc,
                    CancellationToken.None);
                var activeState = CreateActiveGuiBootstrapState(
                    registration,
                    new SpyUnityIpcServer(),
                    CreateUnityLogCaptureService(),
                    new SpyServiceProvider(),
                    new StaticAvailabilityObservationSource(readyObservation),
                    new ImmediateUnityMutationLaneControl(),
                    sidecarObserver,
                    writer);
                var scheduleMethod = typeof(UnityGuiBootstrap).GetMethod(
                                         "TryScheduleRecoveryLifecycleSidecarForDomainReload",
                                         BindingFlags.Static | BindingFlags.NonPublic)
                                     ?? throw new InvalidOperationException(
                                         "Domain reload recovery scheduling method was not found.");
                var arguments = new[] { activeState, (object)0L };

                var scheduled = (bool)scheduleMethod.Invoke(null, arguments);
                Assert.That(scheduled, Is.True);
                var version = (long)arguments[1];
                await writer.FlushAsync(version, CancellationToken.None);

                Assert.That(
                    sharedState.LatestObservation.State.LifecycleState,
                    Is.EqualTo(IpcEditorLifecycleState.Recovering));
                Assert.That(sharedState.LatestRecoveryLease, Is.Not.Null);
                Assert.That(
                    sharedState.LatestRecoveryLease.SessionGenerationId,
                    Is.EqualTo(registration.SessionGenerationId));
                Assert.That(
                    sharedState.LatestRecoveryLease.ExpiresAtUtc - sharedState.LatestObservation.ObservedAtUtc,
                    Is.EqualTo(DaemonLifecycleObservationTimings.DomainReloadRecoveryLeaseDuration));
            }
            finally
            {
                sidecarObserver.Dispose();
                await writer.StopAsync(CancellationToken.None);
                if (registration != null)
                {
                    UnityGuiSessionPersistence.Delete(registration);
                }

                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CleanupFailedStart_WhenStoppedSidecarInvalidationFails_DoesNotBlockReplacement () => UniTask.ToCoroutine(async () =>
        {
            var persistence = new ControllableLifecycleSidecarPersistence(
                new SharedLifecycleSidecarState(),
                delaySecondWrite: false,
                failDeletion: true);
            var writer = new UnityLifecycleSidecarWriter(persistence);
            var initialObservation = CreateReadyObservation(
                new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
                generation: 1);
            await writer.InitializeAsync(
                initialObservation,
                initialObservation.ObservedAtUtc,
                CancellationToken.None);
            var serviceProvider = new SpyServiceProvider();

            var stoppedSafely = await UnityGuiBootstrap.CleanupFailedStartAsync(
                registration: null,
                server: new SpyUnityIpcServer(),
                lifecycleSidecarWriter: writer,
                unityLogCaptureService: null,
                serviceProvider,
                daemonLogger: NoOpDaemonLogger.Instance);

            Assert.That(stoppedSafely, Is.True);
            Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
            await TestAwaiter.WaitAsync(
                UniTask.WaitUntil(() => persistence.DeleteCount == 3),
                "Owned sidecar invalidation retries",
                TimeSpan.FromSeconds(5));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReleaseResourcesForEditorLifecycleEvent_WhenServerStopDoesNotComplete_UsesSynchronousReleaseOnly () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                var registration = await PrepareAndPublishSessionAsync(storageRoot);
                var incompleteStopSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var server = new SpyUnityIpcServer(stopTask: incompleteStopSource.Task);
                var logCapture = new SpyDisposable();
                var serviceProvider = new SpyServiceProvider();
                var callingThreadId = Thread.CurrentThread.ManagedThreadId;

                UnityGuiBootstrap.ReleaseResourcesForEditorLifecycleEvent(
                    registration,
                    server,
                    logCapture,
                    serviceProvider,
                    NoOpDaemonLogger.Instance,
                    deleteSession: true);

                Assert.That(server.StopCallCount, Is.EqualTo(0));
                Assert.That(server.ReleaseCallCount, Is.EqualTo(1));
                Assert.That(logCapture.DisposeCallCount, Is.EqualTo(1));
                Assert.That(logCapture.DisposeThreadId, Is.EqualTo(callingThreadId));
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeThreadId, Is.EqualTo(callingThreadId));
                Assert.That(File.Exists(registration.SessionPath), Is.False);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReleaseResourcesForEditorLifecycleEvent_WhenServerWasNotCreated_DisposesServiceProviderOnCallingThread () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                var registration = await PrepareAndPublishSessionAsync(storageRoot);
                var serviceProvider = new SpyServiceProvider();
                var callingThreadId = Thread.CurrentThread.ManagedThreadId;

                UnityGuiBootstrap.ReleaseResourcesForEditorLifecycleEvent(
                    registration,
                    server: null,
                    unityLogCaptureService: null,
                    serviceProvider,
                    NoOpDaemonLogger.Instance,
                    deleteSession: true);

                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeThreadId, Is.EqualTo(callingThreadId));
                Assert.That(File.Exists(registration.SessionPath), Is.False);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartingGeneration_WhenEditorLifecycleReleases_RetainsPublicationLeaseUntilWriteTerminates () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            UnityGuiSessionPersistence.PreparedSession preparedSession = null;
            try
            {
                preparedSession = await UnityGuiSessionPersistence.PrepareAsync(
                    storageRoot,
                    ProjectFingerprintTestFactory.Create("fingerprint-starting-lifecycle"),
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-bootstrap-starting-lifecycle"),
                    UnityGuiBootstrapSessionOptions.Create(null),
                    EditorInstanceId,
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession,
                    CancellationToken.None);
                var registration = await UnityGuiSessionPersistence.PublishAsync(
                    preparedSession,
                    CancellationToken.None);
                var server = new SpyUnityIpcServer();
                var logCapture = new SpyDisposable();
                var serviceProvider = new SpyServiceProvider();
                var state = new UnityGuiBootstrap.StartingGuiBootstrapState(
                    CancellationToken.None,
                    EditorInstanceId,
                    LifecycleSidecarGenerationId,
                    NoOpDaemonLogger.Instance);
                var publicationCompletionSource = new TaskCompletionSource<UnityGuiSessionRegistration>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                state.AttachPreparedSession(preparedSession);
                state.AttachResources(server, logCapture, serviceProvider);
                state.AttachSessionPublicationTask(publicationCompletionSource.Task);

                Assert.That(state.TryClaimEditorLifecycleRelease(), Is.True);
                UnityGuiBootstrap.ReleaseStartingStateForEditorLifecycleEvent(state);
                UnityGuiBootstrap.ReleaseStartingStateForEditorLifecycleEvent(state);

                await TestAwaiter.WaitAsync(
                    state.CancellationFinalization,
                    "Starting GUI cancellation finalization",
                    TimeSpan.FromSeconds(5));

                Assert.That(state.CancellationToken.IsCancellationRequested, Is.True);
                Assert.That(server.ReleaseCallCount, Is.EqualTo(1));
                Assert.That(logCapture.DisposeCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
                Assert.That(File.Exists(registration.SessionPath), Is.True);
                Assert.Throws<InvalidOperationException>(preparedSession.ThrowIfCannotPublish);

                publicationCompletionSource.SetResult(registration);
                await TestAwaiter.WaitAsync(
                    state.PreparedSessionFinalization,
                    "Starting GUI publication lease finalization",
                    TimeSpan.FromSeconds(5));

                Assert.That(File.Exists(registration.SessionPath), Is.False);
                Assert.Throws<ObjectDisposedException>(preparedSession.ThrowIfCannotPublish);
                Assert.That(state.TryClaimNormalCleanup(), Is.False);
            }
            finally
            {
                preparedSession?.Dispose();
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartingGeneration_WhenCancellationCallbackBlocks_RunsCancellationOffUnityThread () => UniTask.ToCoroutine(async () =>
        {
            var state = new UnityGuiBootstrap.StartingGuiBootstrapState(
                CancellationToken.None,
                EditorInstanceId,
                LifecycleSidecarGenerationId,
                NoOpDaemonLogger.Instance);
            var server = new SpyUnityIpcServer();
            var logCapture = new SpyDisposable();
            var serviceProvider = new SpyServiceProvider();
            var callbackStartedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackReleaseSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackThreadId = 0;
            var unityThreadId = Thread.CurrentThread.ManagedThreadId;
            state.AttachResources(server, logCapture, serviceProvider);
            using var callbackRegistration = state.CancellationToken.Register(() =>
            {
                Volatile.Write(ref callbackThreadId, Thread.CurrentThread.ManagedThreadId);
                callbackStartedSource.TrySetResult(true);
                callbackReleaseSource.Task.GetAwaiter().GetResult();
            });

            try
            {
                Assert.That(state.TryClaimEditorLifecycleRelease(), Is.True);
                UnityGuiBootstrap.ReleaseStartingStateForEditorLifecycleEvent(state);
                await TestAwaiter.WaitAsync(
                    callbackStartedSource.Task,
                    "Starting GUI cancellation callback start",
                    TimeSpan.FromSeconds(5));

                Assert.That(Volatile.Read(ref callbackThreadId), Is.Not.EqualTo(unityThreadId));
                Assert.That(server.ReleaseCallCount, Is.EqualTo(1));
                Assert.That(logCapture.DisposeCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
            }
            finally
            {
                callbackReleaseSource.TrySetResult(true);
            }

            await TestAwaiter.WaitAsync(
                state.CancellationFinalization,
                "Starting GUI cancellation completion",
                TimeSpan.FromSeconds(5));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PreparedReplacementToken_BeforePublication_RejectsPersistedPreviousGenerationToken () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            UnityGuiSessionPersistence.PreparedSession replacementSession = null;
            try
            {
                var endpoint = new IpcEndpoint(
                    IpcTransportKind.NamedPipe,
                    "ucli-gui-bootstrap-token-rotation");
                UnityGuiSessionRegistration previousRegistration;
                using (var previousSession = await UnityGuiSessionPersistence.PrepareAsync(
                           storageRoot,
                           ProjectFingerprintTestFactory.Create("fingerprint-token-rotation"),
                           endpoint,
                           UnityGuiBootstrapSessionOptions.Create(null),
                           EditorInstanceId,
                           UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession,
                           CancellationToken.None))
                {
                    previousRegistration = await UnityGuiSessionPersistence.PublishAsync(
                        previousSession,
                        CancellationToken.None);
                }

                replacementSession = await UnityGuiSessionPersistence.PrepareAsync(
                    storageRoot,
                    ProjectFingerprintTestFactory.Create("fingerprint-token-rotation"),
                    endpoint,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    EditorInstanceId,
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession,
                    CancellationToken.None);
                var validator = new ExactSessionTokenValidator(
                    replacementSession.Registration.SessionToken);
                var requestHandler = new UnityIpcRequestHandler(
                    validator,
                    new UnexpectedMethodDispatcher(),
                    NoOpDaemonLogger.Instance);
                var previousGenerationPing = new IpcRequestEnvelope(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: Guid.NewGuid(),
                    sessionToken: previousRegistration.SessionToken.GetEncodedValue(),
                    method: ContractLiteralCodec.ToValue(UnityIpcMethod.Ping),
                    payload: IpcPayloadCodec.SerializeToElement(new IpcPingRequest("tests")),
                    responseMode: "single",
                    requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                    requestDeadlineRemainingMilliseconds: 30_000);

                using var phaseScope = new IpcRequestPhaseScopeFactory().Create(
                    previousGenerationPing,
                    CancellationToken.None,
                    TimeSpan.FromSeconds(1));
                var previousGenerationValidation = await requestHandler.ValidateAsync(
                    previousGenerationPing,
                    phaseScope);
                var previousGenerationResponse = previousGenerationValidation.ErrorResponse;
                var replacementTokenAccepted = await validator.ValidateAsync(
                    replacementSession.Registration.SessionToken,
                    CancellationToken.None);

                Assert.That(File.Exists(previousRegistration.SessionPath), Is.True);
                Assert.That(previousGenerationResponse.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(previousGenerationResponse.Errors.Count, Is.EqualTo(1));
                Assert.That(
                    previousGenerationResponse.Errors[0].Code,
                    Is.EqualTo(IpcSessionErrorCodes.SessionTokenInvalid));
                Assert.That(replacementTokenAccepted, Is.True);
            }
            finally
            {
                replacementSession?.Dispose();
                DeleteDirectory(storageRoot);
            }
        });

        private static string CreateStorageRoot ()
        {
            return Path.Combine(Path.GetTempPath(), $"ucli-gui-bootstrap-tests-{Guid.NewGuid():N}");
        }

        private static FieldInfo GetGuiBootstrapStateField (string fieldName)
        {
            return typeof(UnityGuiBootstrap).GetField(
                       fieldName,
                       BindingFlags.Static | BindingFlags.NonPublic)
                   ?? throw new InvalidOperationException(
                       $"Unity GUI bootstrap state field '{fieldName}' was not found.");
        }

        private static object CreateActiveGuiBootstrapState (
            UnityGuiSessionRegistration registration,
            IUnityIpcServer server,
            UnityLogCaptureService unityLogCaptureService,
            IServiceProvider serviceProvider,
            IUnityEditorAvailabilityObservationSource availabilityObservationSource,
            IUnityMutationLaneControl mutationLaneControl,
            UnityMutationLifecycleSidecarObserver mutationLifecycleSidecarObserver,
            UnityLifecycleSidecarWriter lifecycleSidecarWriter)
        {
            var stateType = typeof(UnityGuiBootstrap).GetNestedType(
                                "ActiveGuiBootstrapState",
                                BindingFlags.NonPublic)
                            ?? throw new InvalidOperationException(
                                "Unity GUI active bootstrap state type was not found.");
            return Activator.CreateInstance(
                       stateType,
                       BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                       binder: null,
                       args: new object[]
                       {
                           registration,
                           server,
                           new DaemonShutdownSignal(),
                           unityLogCaptureService,
                           serviceProvider,
                           NoOpDaemonLogger.Instance,
                           availabilityObservationSource,
                           mutationLaneControl,
                           mutationLifecycleSidecarObserver,
                           lifecycleSidecarWriter,
                       },
                       culture: null)
                   ?? throw new InvalidOperationException(
                       "Unity GUI active bootstrap state could not be created.");
        }

        private static UnityLogCaptureService CreateUnityLogCaptureService ()
        {
            return new UnityLogCaptureService(new UnityLogCollector(
                new UnityLogRingBuffer(),
                new UnityCompileMessageDedupeCache(new ManualMonotonicClock()),
                new UnityLogRedactionScopeProvider()));
        }

        private static async UniTask<UnityGuiSessionRegistration> PrepareAndPublishSessionAsync (string storageRoot)
        {
            using var preparedSession = await UnityGuiSessionPersistence.PrepareAsync(
                storageRoot,
                ProjectFingerprintTestFactory.Create("fingerprint"),
                new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-bootstrap-tests"),
                UnityGuiBootstrapSessionOptions.Create(null),
                EditorInstanceId,
                UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession,
                CancellationToken.None);
            return await UnityGuiSessionPersistence.PublishAsync(preparedSession, CancellationToken.None);
        }

        private static UnityEditorObservation CreateReadyObservation (
            DateTimeOffset observedAtUtc,
            long generation)
        {
            return new UnityEditorObservation(
                state: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Gui,
                    lifecycleState: IpcEditorLifecycleState.Ready,
                    compileState: IpcCompileState.Ready,
                    generations: new IpcUnityGenerationSnapshot(generation, generation, 0, 0),
                    playMode: new IpcPlayModeSnapshot(
                        IpcPlayModeState.Stopped,
                        IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                observedAtUtc);
        }

        private static void DeleteDirectory (string storageRoot)
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }

        private sealed class SharedLifecycleSidecarState
        {
            private readonly object syncRoot = new object();

            private UnityEditorObservation latestObservation;

            private DaemonLifecycleRecoveryLease latestRecoveryLease;

            public UnityEditorObservation LatestObservation
            {
                get
                {
                    lock (syncRoot)
                    {
                        return latestObservation;
                    }
                }
            }

            public DaemonLifecycleRecoveryLease LatestRecoveryLease
            {
                get
                {
                    lock (syncRoot)
                    {
                        return latestRecoveryLease;
                    }
                }
            }

            public void Write (
                UnityEditorObservation observation,
                DaemonLifecycleRecoveryLease recoveryLease)
            {
                lock (syncRoot)
                {
                    latestObservation = observation;
                    latestRecoveryLease = recoveryLease;
                }
            }
        }

        private sealed class ControllableLifecycleSidecarPersistence : IUnityLifecycleSidecarPersistence
        {
            private readonly SharedLifecycleSidecarState sharedState;

            private readonly bool delaySecondWrite;

            private readonly bool failDeletion;

            private readonly TaskCompletionSource<bool> delayedWriteStartedSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> delayedWriteReleaseSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int writeCount;

            private int deleteCount;

            public ControllableLifecycleSidecarPersistence (
                SharedLifecycleSidecarState sharedState,
                bool delaySecondWrite,
                bool failDeletion)
            {
                this.sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
                this.delaySecondWrite = delaySecondWrite;
                this.failDeletion = failDeletion;
            }

            public Task DelayedWriteStarted => delayedWriteStartedSource.Task;

            public int DeleteCount => Volatile.Read(ref deleteCount);

            public async Task WriteAsync (
                UnityEditorObservation snapshot,
                DaemonLifecycleRecoveryLease recoveryLease,
                CancellationToken cancellationToken)
            {
                var currentWriteCount = Interlocked.Increment(ref writeCount);
                if (delaySecondWrite && currentWriteCount == 2)
                {
                    delayedWriteStartedSource.TrySetResult(true);
                    await delayedWriteReleaseSource.Task;
                }

                sharedState.Write(snapshot, recoveryLease);
            }

            public Task DeleteIfOwnedAsync (CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref deleteCount);
                return failDeletion
                    ? Task.FromException(new IOException("sidecar invalidation failed"))
                    : Task.CompletedTask;
            }

            public void ReleaseDelayedWrite ()
            {
                delayedWriteReleaseSource.TrySetResult(true);
            }
        }

        private sealed class StubMutationRequestExecutionStartSource : IUnityMutationRequestExecutionStartSource
        {
            public event Func<CancellationToken, Task> RequestExecutionStarting
            {
                add { }
                remove { }
            }
        }

        private sealed class StaticAvailabilityObservationSource : IUnityEditorAvailabilityObservationSource
        {
            private readonly UnityEditorObservation observation;

            public StaticAvailabilityObservationSource (UnityEditorObservation observation)
            {
                this.observation = observation ?? throw new ArgumentNullException(nameof(observation));
            }

            public UnityEditorObservation CaptureAvailabilityObservation ()
            {
                return observation;
            }
        }

        private sealed class SpyUnityIpcServer : IUnityIpcServer
        {
            private readonly Action<IpcEndpoint> onStart;

            private readonly bool throwOnStop;

            private readonly Task stopTask;

            public SpyUnityIpcServer (
                bool throwOnStop = false,
                Task stopTask = null,
                Action<IpcEndpoint> onStart = null)
            {
                this.throwOnStop = throwOnStop;
                this.stopTask = stopTask;
                this.onStart = onStart;
            }

            public int StartCallCount { get; private set; }

            public int StopCallCount { get; private set; }

            public int ReleaseCallCount { get; private set; }

            public Task<IUnityIpcServerPublicationFence> StartAsync (
                IpcEndpoint endpoint,
                CancellationToken cancellationToken = default)
            {
                StartCallCount++;
                onStart?.Invoke(endpoint);
                return Task.FromResult<IUnityIpcServerPublicationFence>(
                    new SpyUnityIpcServerPublicationFence());
            }

            public Task StopAsync (CancellationToken cancellationToken = default)
            {
                StopCallCount++;
                if (throwOnStop)
                {
                    throw new InvalidOperationException("stop failed");
                }

                return stopTask ?? Task.CompletedTask;
            }

            public void ReleaseForEditorLifecycleEvent ()
            {
                ReleaseCallCount++;
                if (throwOnStop)
                {
                    throw new InvalidOperationException("release failed");
                }
            }

            public Task WaitForTerminationAsync (CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class SpyUnityIpcServerPublicationFence : IUnityIpcServerPublicationFence
        {
            private bool committed;

            public void ThrowIfGenerationTerminated ()
            {
            }

            public bool TryCommitActiveOwnership (Action commitActiveOwnership)
            {
                if (commitActiveOwnership == null)
                {
                    throw new ArgumentNullException(nameof(commitActiveOwnership));
                }

                if (committed)
                {
                    throw new InvalidOperationException("Publication ownership was already committed.");
                }

                committed = true;
                commitActiveOwnership();
                return true;
            }

            public void Dispose ()
            {
            }
        }

        private sealed class UnexpectedMethodDispatcher : IUnityIpcMethodDispatcher
        {
            public Task<IpcResponse> DispatchAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestPhaseScope phaseScope)
            {
                throw new InvalidOperationException("An unauthorized request must not be dispatched.");
            }

            public Task<IpcResponse> DispatchStreamingAsync (
                ValidatedUnityIpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                IpcRequestPhaseScope phaseScope)
            {
                throw new InvalidOperationException("An unauthorized request must not be dispatched.");
            }
        }

        private sealed class SpyDisposable : IDisposable
        {
            public int DisposeCallCount { get; private set; }

            public int DisposeThreadId { get; private set; }

            public void Dispose ()
            {
                DisposeCallCount++;
                DisposeThreadId = Thread.CurrentThread.ManagedThreadId;
            }
        }

        private sealed class SpyServiceProvider : IServiceProvider, IDisposable
        {
            private readonly bool throwOnDispose;

            private readonly IUnityMutationLaneControl mutationLaneControl;

            public SpyServiceProvider (
                bool throwOnDispose = false,
                IUnityMutationLaneControl mutationLaneControl = null)
            {
                this.throwOnDispose = throwOnDispose;
                this.mutationLaneControl = mutationLaneControl;
            }

            public int DisposeCallCount { get; private set; }

            public int DisposeThreadId { get; private set; }

            public object GetService (Type serviceType)
            {
                return serviceType == typeof(IUnityMutationLaneControl)
                    ? mutationLaneControl
                    : null;
            }

            public void Dispose ()
            {
                DisposeCallCount++;
                DisposeThreadId = Thread.CurrentThread.ManagedThreadId;
                if (throwOnDispose)
                {
                    throw new InvalidOperationException("dispose failed");
                }
            }
        }

        private sealed class DeferredRetirementMutationLaneControl : IUnityMutationLaneControl
        {
            private readonly TaskCompletionSource<bool> retirementCompletionSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public bool IsBusy => true;

            public bool HasUnfinishedWork => !retirementCompletionSource.Task.IsCompleted;

            public bool IsQuarantined => true;

            public IUnityMutationActivity BeginMutation ()
            {
                throw new InvalidOperationException("Retirement test must not begin a mutation.");
            }

            public void Quarantine (string reason, Task mutationCompletion)
            {
                throw new InvalidOperationException("Retirement test must not quarantine the lane.");
            }

            public bool TrySealAdmissionForRetirement (out IDisposable admissionSeal)
            {
                throw new InvalidOperationException("Retirement test must not seal admission.");
            }

            public Task WaitForRetirementAsync ()
            {
                return retirementCompletionSource.Task;
            }

            public void CompleteRetirement ()
            {
                retirementCompletionSource.TrySetResult(true);
            }
        }
    }
}
