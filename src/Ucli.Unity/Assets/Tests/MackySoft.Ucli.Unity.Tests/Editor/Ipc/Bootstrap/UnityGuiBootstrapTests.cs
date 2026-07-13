using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGuiBootstrapTests
    {
        private static readonly Guid EditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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

        [TestCase((int)UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession, false, false)]
        [TestCase((int)UnityGuiSessionReplacementScope.AnyCurrentProcessSession, false, true)]
        [TestCase((int)UnityGuiSessionReplacementScope.AnyCurrentProcessSession, true, false)]
        [Category("Size.Small")]
        public void CanReplaceActiveSession_RequiresExplicitScopeAndIdleMutationLane (
            int replacementScopeValue,
            bool isMutationBusy,
            bool expected)
        {
            Assert.That(
                UnityGuiBootstrap.CanReplaceActiveSession(
                    (UnityGuiSessionReplacementScope)replacementScopeValue,
                    isMutationBusy),
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
                var previousGenerationPing = new IpcRequest(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: Guid.NewGuid(),
                    sessionToken: previousRegistration.SessionToken.GetEncodedValue(),
                    method: ContractLiteralCodec.ToValue(UnityIpcMethod.Ping),
                    payload: IpcPayloadCodec.SerializeToElement(new IpcPingRequest("tests")),
                    responseMode: "single");

                var previousGenerationResponse = await requestHandler.HandleAsync(
                    previousGenerationPing,
                    CancellationToken.None);
                var replacementTokenAccepted = await validator.ValidateAsync(
                    replacementSession.Registration.SessionToken.GetEncodedValue(),
                    CancellationToken.None);

                Assert.That(File.Exists(previousRegistration.SessionPath), Is.True);
                Assert.That(previousGenerationResponse.Status, Is.EqualTo(IpcProtocol.StatusError));
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

        private static void DeleteDirectory (string storageRoot)
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
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
                IpcRequest request,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("An unauthorized request must not be dispatched.");
            }

            public Task<IpcResponse> DispatchStreamingAsync (
                IpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                CancellationToken cancellationToken = default)
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

            public SpyServiceProvider (bool throwOnDispose = false)
            {
                this.throwOnDispose = throwOnDispose;
            }

            public int DisposeCallCount { get; private set; }

            public int DisposeThreadId { get; private set; }

            public object GetService (Type serviceType)
            {
                return null;
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
    }
}
