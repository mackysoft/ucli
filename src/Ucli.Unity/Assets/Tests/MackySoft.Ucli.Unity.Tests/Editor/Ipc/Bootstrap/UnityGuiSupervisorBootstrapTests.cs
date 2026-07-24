using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGuiSupervisorBootstrapTests
    {
        [Test]
        [Category("Size.Small")]
        public void AddGuiSupervisorHostServices_RegistersRequestDeadlineScopeFactory ()
        {
            var services = new ServiceCollection();
            services.AddUnityGuiSupervisorHostServices(
                new PermitAllSessionTokenValidator(),
                ProjectFingerprintTestFactory.Create("gui-supervisor-services"),
                UnityIpcEndpointBinding.Create(
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-supervisor-services")),
                NoOpDaemonLogger.Instance);

            using var serviceProvider = services.BuildServiceProvider();

            Assert.That(
                serviceProvider.GetService<IIpcRequestPhaseScopeFactory>(),
                Is.TypeOf<IpcRequestPhaseScopeFactory>());
        }

        [Test]
        [Category("Size.Small")]
        public void RestartScheduler_WhenInitialBindFails_RestartsOnlyAfterBackoff ()
        {
            var time = 100d;
            var updateLoop = new RecordingEditorUpdateLoop();
            var restartCount = 0;
            var scheduler = new UnityGuiSupervisorRestartScheduler(
                () => time,
                updateLoop.Subscribe,
                updateLoop.Unsubscribe,
                static () => true,
                () => restartCount++);

            scheduler.ScheduleAfterSafeCleanup();

            updateLoop.Invoke();
            Assert.That(restartCount, Is.EqualTo(0));

            time += UnityGuiSupervisorRestartScheduler.InitialRestartDelay.TotalSeconds;
            updateLoop.Invoke();

            Assert.That(restartCount, Is.EqualTo(1));
            Assert.That(updateLoop.IsSubscribed, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void RestartScheduler_WhenActiveListenerFaults_RepublishesWithNewManifestToken ()
        {
            var time = 200d;
            var updateLoop = new RecordingEditorUpdateLoop();
            var publication = new RecordingSupervisorPublication();
            publication.Publish();
            var initialSessionToken = publication.SessionToken;
            var scheduler = new UnityGuiSupervisorRestartScheduler(
                () => time,
                updateLoop.Subscribe,
                updateLoop.Unsubscribe,
                static () => true,
                publication.Publish);

            var activeGeneration = new object();
            scheduler.BeginGenerationStabilityObservation(activeGeneration);
            scheduler.EndGenerationStabilityObservation(activeGeneration);
            scheduler.ScheduleAfterSafeCleanup();
            time += UnityGuiSupervisorRestartScheduler.InitialRestartDelay.TotalSeconds;
            updateLoop.Invoke();

            Assert.That(publication.PublishCount, Is.EqualTo(2));
            Assert.That(publication.SessionToken, Is.Not.EqualTo(initialSessionToken));
        }

        [Test]
        [Category("Size.Small")]
        public void RestartScheduler_WhenEditorLifecycleStops_DoesNotRestartOrResubscribe ()
        {
            var time = 300d;
            var updateLoop = new RecordingEditorUpdateLoop();
            var restartCount = 0;
            var scheduler = new UnityGuiSupervisorRestartScheduler(
                () => time,
                updateLoop.Subscribe,
                updateLoop.Unsubscribe,
                static () => true,
                () => restartCount++);

            scheduler.ScheduleAfterSafeCleanup();
            var staleUpdateCallback = updateLoop.SubscribedCallback;
            scheduler.StopForEditorLifecycleEvent();
            time += UnityGuiSupervisorRestartScheduler.MaximumRestartDelay.TotalSeconds;
            staleUpdateCallback();
            scheduler.ScheduleAfterSafeCleanup();

            Assert.That(restartCount, Is.EqualTo(0));
            Assert.That(updateLoop.IsSubscribed, Is.False);
            Assert.That(updateLoop.SubscribeCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Size.Small")]
        public void RestartScheduler_WhenBootstrapRestartIsBlocked_DoesNotSchedule ()
        {
            var updateLoop = new RecordingEditorUpdateLoop();
            var scheduler = new UnityGuiSupervisorRestartScheduler(
                static () => 400d,
                updateLoop.Subscribe,
                updateLoop.Unsubscribe,
                static () => false,
                static () => Assert.Fail("Blocked supervisor restart must not execute."));

            scheduler.ScheduleAfterSafeCleanup();

            Assert.That(updateLoop.IsSubscribed, Is.False);
            Assert.That(updateLoop.SubscribeCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Size.Small")]
        public void RestartScheduler_WhenPublishedGenerationsFailBeforeStabilityWindow_IncreasesAndCapsBackoff ()
        {
            var time = 500d;
            var updateLoop = new RecordingEditorUpdateLoop();
            var restartCount = 0;
            var scheduler = new UnityGuiSupervisorRestartScheduler(
                () => time,
                updateLoop.Subscribe,
                updateLoop.Unsubscribe,
                static () => true,
                () => restartCount++);
            var expectedDelays = new[]
            {
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMilliseconds(400),
                TimeSpan.FromMilliseconds(800),
                TimeSpan.FromMilliseconds(1600),
                TimeSpan.FromMilliseconds(3200),
                TimeSpan.FromMilliseconds(5000),
                TimeSpan.FromMilliseconds(5000),
            };

            for (var attempt = 0; attempt < expectedDelays.Length; attempt++)
            {
                var activeGeneration = new object();
                scheduler.BeginGenerationStabilityObservation(activeGeneration);
                scheduler.EndGenerationStabilityObservation(activeGeneration);
                scheduler.ScheduleAfterSafeCleanup();

                time += expectedDelays[attempt].TotalSeconds * 0.9d;
                updateLoop.Invoke();
                Assert.That(restartCount, Is.EqualTo(attempt));

                time += expectedDelays[attempt].TotalSeconds * 0.2d;
                updateLoop.Invoke();
                Assert.That(restartCount, Is.EqualTo(attempt + 1));
            }

            Assert.That(restartCount, Is.EqualTo(expectedDelays.Length));
        }

        [Test]
        [Category("Size.Small")]
        public void RestartScheduler_WhenGenerationRemainsActiveForStabilityWindow_ResetsBackoff ()
        {
            var time = 600d;
            var updateLoop = new RecordingEditorUpdateLoop();
            var restartCount = 0;
            var scheduler = new UnityGuiSupervisorRestartScheduler(
                () => time,
                updateLoop.Subscribe,
                updateLoop.Unsubscribe,
                static () => true,
                () => restartCount++);

            for (var attempt = 0; attempt < 3; attempt++)
            {
                scheduler.ScheduleAfterSafeCleanup();
                time += UnityGuiSupervisorRestartScheduler.ResolveRestartDelay(attempt).TotalSeconds;
                updateLoop.Invoke();
            }

            var stableGeneration = new object();
            scheduler.BeginGenerationStabilityObservation(stableGeneration);
            time += UnityGuiSupervisorRestartScheduler.StableGenerationWindow.TotalSeconds;
            scheduler.EndGenerationStabilityObservation(stableGeneration);
            scheduler.ScheduleAfterSafeCleanup();

            time += UnityGuiSupervisorRestartScheduler.InitialRestartDelay.TotalSeconds / 2d;
            updateLoop.Invoke();
            Assert.That(restartCount, Is.EqualTo(3));

            time += UnityGuiSupervisorRestartScheduler.InitialRestartDelay.TotalSeconds / 2d
                + TimeSpan.FromMilliseconds(1).TotalSeconds;
            updateLoop.Invoke();
            Assert.That(restartCount, Is.EqualTo(4));
        }

        [Test]
        [Category("Size.Small")]
        public void RestartScheduler_WhenStaleGenerationEnds_DoesNotResetCurrentBackoff ()
        {
            var time = 700d;
            var updateLoop = new RecordingEditorUpdateLoop();
            var restartCount = 0;
            var scheduler = new UnityGuiSupervisorRestartScheduler(
                () => time,
                updateLoop.Subscribe,
                updateLoop.Unsubscribe,
                static () => true,
                () => restartCount++);

            scheduler.ScheduleAfterSafeCleanup();
            time += UnityGuiSupervisorRestartScheduler.InitialRestartDelay.TotalSeconds;
            updateLoop.Invoke();

            var staleGeneration = new object();
            scheduler.BeginGenerationStabilityObservation(staleGeneration);
            time += UnityGuiSupervisorRestartScheduler.StableGenerationWindow.TotalSeconds;

            var currentGeneration = new object();
            scheduler.BeginGenerationStabilityObservation(currentGeneration);
            scheduler.EndGenerationStabilityObservation(staleGeneration);
            scheduler.EndGenerationStabilityObservation(currentGeneration);
            scheduler.ScheduleAfterSafeCleanup();

            time += UnityGuiSupervisorRestartScheduler.InitialRestartDelay.TotalSeconds;
            updateLoop.Invoke();
            Assert.That(restartCount, Is.EqualTo(1));

            time += UnityGuiSupervisorRestartScheduler.InitialRestartDelay.TotalSeconds;
            updateLoop.Invoke();
            Assert.That(restartCount, Is.EqualTo(2));
        }

        [Test]
        [Category("Size.Small")]
        public void RestartScheduler_WhenEditorLifecycleStops_DiscardsStabilityObservation ()
        {
            var time = 800d;
            var updateLoop = new RecordingEditorUpdateLoop();
            var restartCount = 0;
            var scheduler = new UnityGuiSupervisorRestartScheduler(
                () => time,
                updateLoop.Subscribe,
                updateLoop.Unsubscribe,
                static () => true,
                () => restartCount++);
            var activeGeneration = new object();

            scheduler.BeginGenerationStabilityObservation(activeGeneration);
            scheduler.StopForEditorLifecycleEvent();
            time += UnityGuiSupervisorRestartScheduler.StableGenerationWindow.TotalSeconds;
            scheduler.EndGenerationStabilityObservation(activeGeneration);
            scheduler.ScheduleAfterSafeCleanup();

            Assert.That(restartCount, Is.EqualTo(0));
            Assert.That(updateLoop.IsSubscribed, Is.False);
            Assert.That(updateLoop.SubscribeCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Size.Small")]
        public void StartingGeneration_WhenManifestTokenDoesNotMatchIdentity_RejectsManifest ()
        {
            var projectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");
            var sessionToken = IpcSessionToken.CreateRandom();
            var state = new UnityGuiSupervisorBootstrap.StartingGuiSupervisorState(
                NoOpDaemonLogger.Instance);
            try
            {
                state.AttachIdentity(
                    AbsolutePath.Parse(Path.Combine(Path.GetTempPath(), "storage-root")),
                    projectFingerprint,
                    sessionToken);
                var foreignManifest = new GuiSupervisorManifestJsonContract(
                    SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
                    SessionToken: IpcSessionToken.CreateRandom(),
                    ProjectFingerprint: projectFingerprint,
                    Endpoint: new IpcEndpoint(
                        IpcTransportKind.NamedPipe,
                        "ucli-supervisor-foreign-manifest"),
                    ProcessId: 1,
                    ProcessStartedAtUtc: null,
                    IssuedAtUtc: DateTimeOffset.UtcNow);

                Assert.Throws<InvalidOperationException>(() => state.ValidateManifest(foreignManifest));
            }
            finally
            {
                state.DisposeCancellationSource();
            }
        }

        [Test]
        [Category("Size.Small")]
        public void ReleaseUnattachedStartingResources_WhenServerWasConstructed_ReleasesServerBeforeProvider ()
        {
            var server = new SpyUnityIpcServer(Task.CompletedTask);
            var serviceProvider = new SpyServiceProvider
            {
                OnDispose = () => Assert.That(server.ReleaseCallCount, Is.EqualTo(1)),
            };

            UnityGuiSupervisorBootstrap.ReleaseUnattachedStartingResources(
                server,
                serviceProvider,
                NoOpDaemonLogger.Instance);

            Assert.That(server.ReleaseCallCount, Is.EqualTo(1));
            Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartingGeneration_WhenEditorLifecycleReleases_CancelsAndReleasesOwnedResourcesOnce () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-supervisor-starting");
            var sessionToken = IpcSessionToken.CreateRandom();
            try
            {
                using var publicationLease = await UnityGuiSupervisorPersistence.AcquirePublicationLeaseAsync(
                    storageRoot,
                    projectFingerprint,
                    CancellationToken.None);
                var publicationTask = publicationLease.PublishAsync(
                        new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-supervisor-starting-lifecycle"),
                        sessionToken,
                        DateTimeOffset.UtcNow,
                        CancellationToken.None)
                    .AsTask();
                var manifest = await publicationTask;
                var server = new SpyUnityIpcServer(Task.CompletedTask);
                var serviceProvider = new SpyServiceProvider();
                var controlPlaneRequestLifetime = new DeferredControlPlaneRequestLifetime();
                var state = new UnityGuiSupervisorBootstrap.StartingGuiSupervisorState(
                    NoOpDaemonLogger.Instance);
                state.AttachIdentity(storageRoot, projectFingerprint, sessionToken);
                state.AttachResources(server, serviceProvider, controlPlaneRequestLifetime);
                state.AttachPublicationLease(publicationLease);
                state.AttachManifestPublicationTask(publicationTask);
                state.ValidateManifest(manifest);

                Assert.That(state.TryClaimEditorLifecycleRelease(), Is.True);
                UnityGuiSupervisorBootstrap.ReleaseStartingStateForEditorLifecycleEvent(state);
                UnityGuiSupervisorBootstrap.ReleaseStartingStateForEditorLifecycleEvent(state);
                await state.ManifestPublicationFinalization;

                Assert.That(state.CancellationToken.IsCancellationRequested, Is.True);
                Assert.That(server.ReleaseCallCount, Is.EqualTo(1));
                Assert.That(controlPlaneRequestLifetime.WaitCallCount, Is.EqualTo(0));
                Assert.That(serviceProvider.DisposeCallCount, Is.Zero);
                Assert.That(
                    File.Exists(UcliStoragePathResolver.ResolveGuiSupervisorManifestPath(
                        storageRoot,
                        projectFingerprint).Value),
                    Is.False);
                Assert.That(state.TryClaimNormalCleanup(), Is.False);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StopState_WhenStopAlreadyInProgress_AwaitsTheSameCompletionResult () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-supervisor-stop");
            var sessionToken = IpcSessionToken.CreateRandom();
            try
            {
                using var publicationLease = await UnityGuiSupervisorPersistence.AcquirePublicationLeaseAsync(
                    storageRoot,
                    projectFingerprint,
                    CancellationToken.None);
                var manifest = await publicationLease.PublishAsync(
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-supervisor-duplicate-stop"),
                    sessionToken,
                    DateTimeOffset.UtcNow,
                    CancellationToken.None);
                publicationLease.Dispose();
                var stopCompletionSource = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var server = new SpyUnityIpcServer(stopCompletionSource.Task);
                var serviceProvider = new SpyServiceProvider();
                var controlPlaneRequestLifetime = new DeferredControlPlaneRequestLifetime();
                var state = new UnityGuiSupervisorBootstrap.ActiveGuiSupervisorState(
                    sessionToken,
                    server,
                    serviceProvider,
                    controlPlaneRequestLifetime,
                    NoOpDaemonLogger.Instance,
                    storageRoot,
                    projectFingerprint);

                var firstStopTask = UnityGuiSupervisorBootstrap.StopStateAsync(state);
                var duplicateStopTask = UnityGuiSupervisorBootstrap.StopStateAsync(state);

                Assert.That(server.StopCallCount, Is.EqualTo(1));
                Assert.That(firstStopTask.IsCompleted, Is.False);
                Assert.That(duplicateStopTask.IsCompleted, Is.False);

                stopCompletionSource.SetResult(true);
                await controlPlaneRequestLifetime.WaitObserved;

                Assert.That(firstStopTask.IsCompleted, Is.False);
                Assert.That(duplicateStopTask.IsCompleted, Is.False);
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(0));

                controlPlaneRequestLifetime.CompleteRetirement();
                var firstResult = await firstStopTask;
                var duplicateResult = await duplicateStopTask;

                Assert.That(firstResult, Is.True);
                Assert.That(duplicateResult, Is.True);
                Assert.That(server.StopCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StopState_WhenControlPlaneDoesNotRetire_ReturnsUnsafeAtForegroundDeadline () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            var controlPlaneRequestLifetime = new DeferredControlPlaneRequestLifetime();
            try
            {
                var serviceProvider = new SpyServiceProvider();
                var state = new UnityGuiSupervisorBootstrap.ActiveGuiSupervisorState(
                    IpcSessionToken.CreateRandom(),
                    new SpyUnityIpcServer(Task.CompletedTask),
                    serviceProvider,
                    controlPlaneRequestLifetime,
                    NoOpDaemonLogger.Instance,
                    storageRoot,
                    ProjectFingerprintTestFactory.Create("fingerprint-supervisor-retirement-timeout"));

                var stoppedSafely = await UnityGuiSupervisorBootstrap.StopStateAsync(state);

                Assert.That(stoppedSafely, Is.False);
                Assert.That(controlPlaneRequestLifetime.WaitCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeCallCount, Is.Zero);

                controlPlaneRequestLifetime.CompleteRetirement();
                var releasedSafely = await state.WaitForDeferredResourceReleaseAsync();

                Assert.That(releasedSafely, Is.True);
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
            }
            finally
            {
                controlPlaneRequestLifetime.CompleteRetirement();
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StopState_WhenDeferredProviderDisposeFails_KeepsGenerationUnsafe () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            var controlPlaneRequestLifetime = new DeferredControlPlaneRequestLifetime();
            try
            {
                var serviceProvider = new SpyServiceProvider(throwOnDispose: true);
                var state = new UnityGuiSupervisorBootstrap.ActiveGuiSupervisorState(
                    IpcSessionToken.CreateRandom(),
                    new SpyUnityIpcServer(Task.CompletedTask),
                    serviceProvider,
                    controlPlaneRequestLifetime,
                    NoOpDaemonLogger.Instance,
                    storageRoot,
                    ProjectFingerprintTestFactory.Create("fingerprint-supervisor-deferred-dispose-failure"));

                var stoppedSafely = await UnityGuiSupervisorBootstrap.StopStateAsync(state);
                controlPlaneRequestLifetime.CompleteRetirement();
                var releasedSafely = await state.WaitForDeferredResourceReleaseAsync();
                var repeatedResult = await UnityGuiSupervisorBootstrap.StopStateAsync(state);

                Assert.That(stoppedSafely, Is.False);
                Assert.That(releasedSafely, Is.False);
                Assert.That(repeatedResult, Is.False);
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
            }
            finally
            {
                controlPlaneRequestLifetime.CompleteRetirement();
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StopState_WhenServiceProviderDisposeFails_ReturnsUnsafeWithoutRetry () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            var controlPlaneRequestLifetime = new DeferredControlPlaneRequestLifetime();
            controlPlaneRequestLifetime.CompleteRetirement();
            try
            {
                var serviceProvider = new SpyServiceProvider(throwOnDispose: true);
                var state = new UnityGuiSupervisorBootstrap.ActiveGuiSupervisorState(
                    IpcSessionToken.CreateRandom(),
                    new SpyUnityIpcServer(Task.CompletedTask),
                    serviceProvider,
                    controlPlaneRequestLifetime,
                    NoOpDaemonLogger.Instance,
                    storageRoot,
                    ProjectFingerprintTestFactory.Create("fingerprint-supervisor-dispose-failure"));

                var stoppedSafely = await UnityGuiSupervisorBootstrap.StopStateAsync(state);
                var repeatedResult = await UnityGuiSupervisorBootstrap.StopStateAsync(state);

                Assert.That(stoppedSafely, Is.False);
                Assert.That(repeatedResult, Is.False);
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        private static AbsolutePath CreateStorageRoot ()
        {
            return AbsolutePath.Parse(
                Path.Combine(Path.GetTempPath(), $"ucli-gui-supervisor-bootstrap-tests-{Guid.NewGuid():N}"));
        }

        private static void DeleteDirectory (AbsolutePath storageRoot)
        {
            if (Directory.Exists(storageRoot.Value))
            {
                Directory.Delete(storageRoot.Value, recursive: true);
            }
        }

        private sealed class SpyUnityIpcServer : IUnityIpcServer
        {
            private readonly Task stopTask;

            public SpyUnityIpcServer (Task stopTask)
            {
                this.stopTask = stopTask ?? throw new ArgumentNullException(nameof(stopTask));
            }

            public int StopCallCount { get; private set; }

            public int ReleaseCallCount { get; private set; }

            public Task<IUnityIpcServerPublicationFence> StartAsync (
                UnityIpcEndpointBinding endpointBinding,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IUnityIpcServerPublicationFence>(
                    new SpyUnityIpcServerPublicationFence());
            }

            public Task StopAsync (CancellationToken cancellationToken = default)
            {
                StopCallCount++;
                return stopTask;
            }

            public void ReleaseForEditorLifecycleEvent ()
            {
                ReleaseCallCount++;
            }

            public Task WaitForTerminationAsync (CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingEditorUpdateLoop
        {
            private EditorApplication.CallbackFunction subscribedCallback;

            public int SubscribeCount { get; private set; }

            public bool IsSubscribed => subscribedCallback != null;

            public EditorApplication.CallbackFunction SubscribedCallback =>
                subscribedCallback ?? throw new InvalidOperationException("No editor update callback is subscribed.");

            public void Subscribe (EditorApplication.CallbackFunction callback)
            {
                Assert.That(callback, Is.Not.Null);
                Assert.That(subscribedCallback, Is.Null);
                subscribedCallback = callback;
                SubscribeCount++;
            }

            public void Unsubscribe (EditorApplication.CallbackFunction callback)
            {
                if (subscribedCallback == callback)
                {
                    subscribedCallback = null;
                }
            }

            public void Invoke ()
            {
                subscribedCallback?.Invoke();
            }
        }

        private sealed class RecordingSupervisorPublication
        {
            public int PublishCount { get; private set; }

            public string SessionToken { get; private set; }

            public void Publish ()
            {
                PublishCount++;
                SessionToken = Guid.NewGuid().ToString("N");
            }
        }

        private sealed class DeferredControlPlaneRequestLifetime : IUnityControlPlaneRequestLifetime
        {
            private readonly TaskCompletionSource<bool> retirementCompletionSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> waitObservedCompletionSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public bool HasUnfinishedWork => !retirementCompletionSource.Task.IsCompleted;

            public int WaitCallCount { get; private set; }

            public Task WaitObserved => waitObservedCompletionSource.Task;

            public Task WaitForRetirementAsync ()
            {
                WaitCallCount++;
                waitObservedCompletionSource.TrySetResult(true);
                return retirementCompletionSource.Task;
            }

            public void CompleteRetirement ()
            {
                retirementCompletionSource.TrySetResult(true);
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

        private sealed class SpyServiceProvider : IServiceProvider, IDisposable
        {
            private readonly bool throwOnDispose;

            public SpyServiceProvider (bool throwOnDispose = false)
            {
                this.throwOnDispose = throwOnDispose;
            }

            public int DisposeCallCount { get; private set; }

            public Action OnDispose { get; set; }

            public object GetService (Type serviceType)
            {
                return null;
            }

            public void Dispose ()
            {
                DisposeCallCount++;
                OnDispose?.Invoke();
                if (throwOnDispose)
                {
                    throw new InvalidOperationException("service provider disposal failed");
                }
            }
        }
    }
}
