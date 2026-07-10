using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGuiSupervisorBootstrapTests
    {
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

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartingGeneration_WhenEditorLifecycleReleases_CancelsAndReleasesOwnedResourcesOnce () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            const string ProjectFingerprint = "fingerprint-supervisor-starting";
            const string SessionToken = "session-token-supervisor-starting";
            try
            {
                using var publicationLease = await UnityGuiSupervisorPersistence.AcquirePublicationLeaseAsync(
                    storageRoot,
                    ProjectFingerprint,
                    CancellationToken.None);
                var publicationTask = publicationLease.PublishAsync(
                        new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-supervisor-starting-lifecycle"),
                        SessionToken,
                        DateTimeOffset.UtcNow,
                        CancellationToken.None)
                    .AsTask();
                var manifest = await publicationTask;
                var server = new SpyUnityIpcServer(Task.CompletedTask);
                var serviceProvider = new SpyServiceProvider();
                var state = new UnityGuiSupervisorBootstrap.StartingGuiSupervisorState(
                    NoOpDaemonLogger.Instance);
                state.AttachIdentity(storageRoot, ProjectFingerprint, SessionToken);
                state.AttachResources(server, serviceProvider);
                state.AttachPublicationLease(publicationLease);
                state.AttachManifestPublicationTask(publicationTask);
                state.AttachManifest(manifest);

                Assert.That(state.TryClaimEditorLifecycleRelease(), Is.True);
                UnityGuiSupervisorBootstrap.ReleaseStartingStateForEditorLifecycleEvent(state);
                UnityGuiSupervisorBootstrap.ReleaseStartingStateForEditorLifecycleEvent(state);
                await state.ManifestPublicationFinalization;

                Assert.That(state.CancellationToken.IsCancellationRequested, Is.True);
                Assert.That(server.ReleaseCallCount, Is.EqualTo(1));
                Assert.That(serviceProvider.DisposeCallCount, Is.EqualTo(1));
                Assert.That(
                    File.Exists(UcliStoragePathResolver.ResolveGuiSupervisorManifestPath(
                        storageRoot,
                        ProjectFingerprint)),
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
            const string ProjectFingerprint = "fingerprint-supervisor-stop";
            const string SessionToken = "session-token-supervisor-stop";
            try
            {
                using var publicationLease = await UnityGuiSupervisorPersistence.AcquirePublicationLeaseAsync(
                    storageRoot,
                    ProjectFingerprint,
                    CancellationToken.None);
                var manifest = await publicationLease.PublishAsync(
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-supervisor-duplicate-stop"),
                    SessionToken,
                    DateTimeOffset.UtcNow,
                    CancellationToken.None);
                publicationLease.Dispose();
                var stopCompletionSource = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var server = new SpyUnityIpcServer(stopCompletionSource.Task);
                var serviceProvider = new SpyServiceProvider();
                var state = new UnityGuiSupervisorBootstrap.ActiveGuiSupervisorState(
                    manifest,
                    server,
                    serviceProvider,
                    NoOpDaemonLogger.Instance,
                    storageRoot,
                    ProjectFingerprint);

                var firstStopTask = UnityGuiSupervisorBootstrap.StopStateAsync(state);
                var duplicateStopTask = UnityGuiSupervisorBootstrap.StopStateAsync(state);

                Assert.That(server.StopCallCount, Is.EqualTo(1));
                Assert.That(firstStopTask.IsCompleted, Is.False);
                Assert.That(duplicateStopTask.IsCompleted, Is.False);

                stopCompletionSource.SetResult(true);
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

        private static string CreateStorageRoot ()
        {
            return Path.Combine(Path.GetTempPath(), $"ucli-gui-supervisor-bootstrap-tests-{Guid.NewGuid():N}");
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
            private readonly Task stopTask;

            public SpyUnityIpcServer (Task stopTask)
            {
                this.stopTask = stopTask ?? throw new ArgumentNullException(nameof(stopTask));
            }

            public int StopCallCount { get; private set; }

            public int ReleaseCallCount { get; private set; }

            public Task<IUnityIpcServerPublicationFence> StartAsync (
                IpcEndpoint endpoint,
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
            public int DisposeCallCount { get; private set; }

            public object GetService (Type serviceType)
            {
                return null;
            }

            public void Dispose ()
            {
                DisposeCallCount++;
            }
        }
    }
}
