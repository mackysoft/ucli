using System;
using System.Collections;
using System.IO;
using System.Linq;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Recording;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Recording.Recorder
{
    [TestFixture]
    internal sealed class UnityRecorderLiveRecordingTests
    {
        private IGameViewRecordingAdapter adapter;

        private Guid recordingId;

        private AbsolutePath outputPath;

        [UnityTest]
        [Category("Integration")]
        public IEnumerator StartAndStop_OwnsOneRecordingAndPublishesExactCleanupFacts ()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("Live GameView recording requires a GUI Editor session.");
            }
            if (!string.Equals(Application.unityVersion, "6000.3.11f1", StringComparison.Ordinal))
            {
                Assert.Ignore("The live adapter conformance run is pinned to Unity 6000.3.11f1.");
            }

            yield return new EnterPlayMode();
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            Assert.That(gameViewType, Is.Not.Null);
            var gameView = EditorWindow.GetWindow(gameViewType, utility: false, title: "Game", focus: true);
            gameView.Show();
            gameView.Focus();

            Assert.That(GameViewRecordingAdapterRegistry.Shared.TryGet(out adapter), Is.True);

            GameViewRecordingRuntimeAdmission admission = null;
            for (var attempt = 0; attempt < 120; attempt++)
            {
                admission = adapter.GetRuntimeAdmission();
                if (admission is GameViewRecordingRuntimeReadyAdmission)
                {
                    break;
                }

                yield return null;
            }
            Assert.That(admission, Is.TypeOf<GameViewRecordingRuntimeReadyAdmission>());

            var projectRoot = AbsolutePath.Parse(Directory.GetParent(Application.dataPath).FullName);
            var outputDirectory = ContainedPath.Create(
                projectRoot,
                RootRelativePath.Parse("Library/UcliRecorderTests")).Target;
            Directory.CreateDirectory(outputDirectory.Value);
            recordingId = Guid.NewGuid();
            outputPath = ContainedPath.Create(
                outputDirectory,
                RootRelativePath.Parse($"{recordingId:N}.mp4")).Target;
            var request = new GameViewRecordingStartRequest(
                recordingId,
                Sha256Digest.Parse(new string('b', 64)),
                dimensions: new PixelDimensions(320, 240),
                frameRate: 30,
                maximumDuration: TimeSpan.FromSeconds(10),
                stagingOutputPath: outputPath,
                startBinding: CreateTestStartBinding());
            var originalRunInBackground = Application.runInBackground;
            var originalCaptureFramerate = Time.captureFramerate;
            var originalCaptureDeltaTime = Time.captureDeltaTime;
            var originalTimeScale = Time.timeScale;

            var started = adapter.Start(request);
            var startedObservation = RequireObserved(started);
            Assert.That(startedObservation.State, Is.EqualTo(GameViewRecordingState.Recording));

            var replay = adapter.Start(request);
            Assert.That(RequireObserved(replay).RecordingId, Is.EqualTo(recordingId));
            var conflict = adapter.Start(new GameViewRecordingStartRequest(
                Guid.NewGuid(),
                request.RequestDigest,
                request.Dimensions,
                request.FrameRate,
                request.MaximumDuration,
                request.StagingOutputPath,
                request.StartBinding));
            Assert.That(conflict, Is.TypeOf<GameViewRecordingRejectedOperation>());
            Assert.That(((GameViewRecordingRejectedOperation)conflict).Failure, Is.EqualTo(GameViewRecordingFailure.Conflict));

            for (var frame = 0; frame < 30; frame++)
            {
                yield return null;
            }

            var stopped = adapter.Stop(recordingId);
            var terminal = RequireObserved(stopped);
            for (var attempt = 0; attempt < 600 && !IsTerminal(terminal.State); attempt++)
            {
                yield return null;
                terminal = RequireObserved(adapter.GetStatus(recordingId));
            }

            Assert.That(terminal.State, Is.EqualTo(GameViewRecordingState.Completed), terminal.Message);
            Assert.That(terminal.Cleanup.Disposition, Is.EqualTo(GameViewRecordingCleanupDisposition.Unconfirmed));
            Assert.That(
                terminal.Cleanup.StateRestorations.Select(item => item.Kind),
                Is.EquivalentTo(Enum.GetValues(typeof(GameViewRecordingStateRestorationKind))));
            Assert.That(
                terminal.Cleanup.ResourceReleases.Select(item => item.Kind),
                Is.EquivalentTo(Enum.GetValues(typeof(GameViewRecordingResourceKind))));
            var temporaryOutput = terminal.Cleanup.ResourceReleases.Single(
                item => item.Kind == GameViewRecordingResourceKind.TemporaryOutput);
            Assert.That(temporaryOutput.ReleaseAttempted, Is.False);
            Assert.That(
                temporaryOutput.Disposition,
                Is.EqualTo(GameViewRecordingResourceReleaseDisposition.Unconfirmed));
            var captureSession = terminal.Cleanup.ResourceReleases.Single(
                item => item.Kind == GameViewRecordingResourceKind.CaptureSession);
            Assert.That(
                captureSession.Disposition,
                Is.EqualTo(GameViewRecordingResourceReleaseDisposition.Released));
            var timeState = terminal.Cleanup.StateRestorations.Single(
                item => item.Kind == GameViewRecordingStateRestorationKind.TimeState);
            Assert.That(
                timeState.Disposition,
                Is.EqualTo(GameViewRecordingStateRestorationDisposition.Restored));
            Assert.That(Application.runInBackground, Is.EqualTo(originalRunInBackground));
            Assert.That(Time.captureFramerate, Is.EqualTo(originalCaptureFramerate));
            Assert.That(
                Time.captureDeltaTime,
                Is.EqualTo(originalCaptureDeltaTime).Within(0.000001f));
            Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale).Within(0.000001f));
            Assert.That(terminal.Target.RequestedDimensions, Is.EqualTo(new PixelDimensions(320, 240)));
            Assert.That(terminal.Target.Dimensions, Is.EqualTo(new PixelDimensions(320, 240)));
            Assert.That(terminal.Timing.MonotonicStartedTimestamp, Is.Not.Null);
            Assert.That(terminal.Timing.MonotonicCompletedTimestamp, Is.GreaterThan(
                terminal.Timing.MonotonicStartedTimestamp.Value));
            Assert.That(terminal.Runtime.EncoderName, Is.EqualTo("UnityEditor.Media.MediaEncoder"));
            Assert.That(new FileInfo(outputPath.Value).Length, Is.GreaterThan(0));
        }

        [UnityTest]
        [Category("Integration")]
        public IEnumerator PlayModeExit_MarksRecordingIndeterminateAndReleasesConfirmedResources ()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("Live GameView recording requires a GUI Editor session.");
            }
            if (!string.Equals(Application.unityVersion, "6000.3.11f1", StringComparison.Ordinal))
            {
                Assert.Ignore("The live adapter conformance run is pinned to Unity 6000.3.11f1.");
            }

            yield return new EnterPlayMode();
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            Assert.That(gameViewType, Is.Not.Null);
            var gameView = EditorWindow.GetWindow(gameViewType, utility: false, title: "Game", focus: true);
            gameView.Show();
            gameView.Focus();

            Assert.That(GameViewRecordingAdapterRegistry.Shared.TryGet(out adapter), Is.True);

            GameViewRecordingRuntimeAdmission admission = null;
            for (var attempt = 0; attempt < 120; attempt++)
            {
                admission = adapter.GetRuntimeAdmission();
                if (admission is GameViewRecordingRuntimeReadyAdmission)
                {
                    break;
                }

                yield return null;
            }
            Assert.That(admission, Is.TypeOf<GameViewRecordingRuntimeReadyAdmission>());

            var projectRoot = AbsolutePath.Parse(Directory.GetParent(Application.dataPath).FullName);
            var outputDirectory = ContainedPath.Create(
                projectRoot,
                RootRelativePath.Parse("Library/UcliRecorderTests")).Target;
            Directory.CreateDirectory(outputDirectory.Value);
            recordingId = Guid.NewGuid();
            outputPath = ContainedPath.Create(
                outputDirectory,
                RootRelativePath.Parse($"{recordingId:N}.mp4")).Target;
            var request = new GameViewRecordingStartRequest(
                recordingId,
                Sha256Digest.Parse(new string('c', 64)),
                dimensions: new PixelDimensions(320, 240),
                frameRate: 30,
                maximumDuration: TimeSpan.FromSeconds(10),
                stagingOutputPath: outputPath,
                startBinding: CreateTestStartBinding());

            var started = adapter.Start(request);
            var startedObservation = RequireObserved(started);
            Assert.That(startedObservation.State, Is.EqualTo(GameViewRecordingState.Recording));

            var active = startedObservation;
            for (var attempt = 0; attempt < 120 && active.Target == null; attempt++)
            {
                yield return null;
                active = RequireObserved(adapter.GetStatus(recordingId));
            }
            Assert.That(active.Target, Is.Not.Null);

            EditorApplication.ExitPlaymode();
            for (var attempt = 0;
                attempt < 600 && EditorApplication.isPlaying;
                attempt++)
            {
                yield return null;
            }
            Assert.That(EditorApplication.isPlaying, Is.False);

            var status = adapter.GetStatus(recordingId);
            for (var attempt = 0;
                attempt < 600 && (!TryGetObserved(status, out var observed) || !IsTerminal(observed.State));
                attempt++)
            {
                yield return null;
                status = adapter.GetStatus(recordingId);
            }

            var terminal = RequireObserved(status);
            Assert.That(terminal.State, Is.EqualTo(GameViewRecordingState.Indeterminate), terminal.Message);
            Assert.That(terminal.StopReason, Is.EqualTo(GameViewRecordingStopReason.PlayModeExited));
            Assert.That(terminal.Failure, Is.EqualTo(GameViewRecordingFailure.CleanupFailed));
            Assert.That(
                terminal.Cleanup.Disposition,
                Is.EqualTo(GameViewRecordingCleanupDisposition.Unconfirmed));
            Assert.That(
                terminal.Cleanup.StateRestorations
                    .Where(item =>
                        item.Disposition == GameViewRecordingStateRestorationDisposition.Unconfirmed)
                    .Select(item => item.Kind),
                Is.EquivalentTo(new[]
                {
                    GameViewRecordingStateRestorationKind.PlayModeView,
                    GameViewRecordingStateRestorationKind.GameView,
                    GameViewRecordingStateRestorationKind.Display,
                    GameViewRecordingStateRestorationKind.Presentation,
                    GameViewRecordingStateRestorationKind.TimeState,
                }));
            Assert.That(
                terminal.Cleanup.ResourceReleases
                    .Where(item =>
                        item.Disposition == GameViewRecordingResourceReleaseDisposition.Unconfirmed)
                    .Select(item => item.Kind),
                Is.EquivalentTo(new[]
                {
                    GameViewRecordingResourceKind.CaptureSession,
                    GameViewRecordingResourceKind.TemporaryOutput,
                }));
            var captureSession = terminal.Cleanup.ResourceReleases.Single(
                item => item.Kind == GameViewRecordingResourceKind.CaptureSession);
            Assert.That(
                captureSession.Disposition,
                Is.EqualTo(GameViewRecordingResourceReleaseDisposition.Unconfirmed));
            var lifecycleSubscriptions = terminal.Cleanup.ResourceReleases.Single(
                item => item.Kind == GameViewRecordingResourceKind.LifecycleSubscriptions);
            Assert.That(
                lifecycleSubscriptions.Disposition,
                Is.EqualTo(GameViewRecordingResourceReleaseDisposition.NotAcquired));
            var runtimeRegistration = terminal.Cleanup.ResourceReleases.Single(
                item => item.Kind == GameViewRecordingResourceKind.RuntimeRegistration);
            Assert.That(
                runtimeRegistration.Disposition,
                Is.EqualTo(GameViewRecordingResourceReleaseDisposition.NotAcquired));
            var recordingExclusion = terminal.Cleanup.ResourceReleases.Single(
                item => item.Kind == GameViewRecordingResourceKind.RecordingExclusion);
            Assert.That(
                recordingExclusion.Disposition,
                Is.EqualTo(GameViewRecordingResourceReleaseDisposition.Released));
        }

        [UnityTearDown]
        public IEnumerator TearDown ()
        {
            if (adapter != null && recordingId != Guid.Empty)
            {
                var status = adapter.GetStatus(recordingId);
                if (TryGetObserved(status, out var observed) && !IsTerminal(observed.State))
                {
                    adapter.Stop(recordingId);
                    for (var attempt = 0; attempt < 300; attempt++)
                    {
                        yield return null;
                        status = adapter.GetStatus(recordingId);
                        if (!TryGetObserved(status, out observed) || IsTerminal(observed.State))
                        {
                            break;
                        }
                    }
                }
            }

            if (EditorApplication.isPlaying)
            {
                yield return new ExitPlayMode();
            }

            if (outputPath != null && File.Exists(outputPath.Value))
            {
                File.Delete(outputPath.Value);
            }
        }

        private static bool IsTerminal (GameViewRecordingState state)
        {
            return state is GameViewRecordingState.Completed
                or GameViewRecordingState.Failed
                or GameViewRecordingState.Interrupted
                or GameViewRecordingState.Indeterminate;
        }

        private static GameViewRecordingSnapshot RequireObserved (
            GameViewRecordingOperationResult result)
        {
            return result is GameViewRecordingObservedOperation observed
                ? observed.Recording
                : throw new AssertionException(
                    result is GameViewRecordingRejectedOperation rejected
                        ? rejected.Message
                        : "The recording adapter returned an unsupported operation result.");
        }

        private static bool TryGetObserved (
            GameViewRecordingOperationResult result,
            out GameViewRecordingSnapshot snapshot)
        {
            snapshot = (result as GameViewRecordingObservedOperation)?.Recording;
            return snapshot != null;
        }

        private static IpcGameViewRecordingStartBinding CreateTestStartBinding ()
        {
            return new IpcGameViewRecordingStartBinding(
                new ProcessIdentity(1, 1),
                new GameViewRecordingRuntimeIdentity(
                    Guid.NewGuid(),
                    "Windows",
                    "Unity Media Encoder",
                    Application.unityVersion),
                new UnityEditorGenerationSnapshot(1, 1, 1, 1));
        }
    }
}
