using System;
using System.Linq;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Recording;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Recording
{
    [TestFixture]
    internal sealed class GameViewRecordingSessionSnapshotStoreTests
    {
        [Test]
        public void SaveAndLoad_PreservesTerminalStatusAcrossSerializationBoundary ()
        {
            var recordingId = Guid.NewGuid();
            var digest = Sha256Digest.Parse(new string('a', 64));
            var completedAtUtc = DateTimeOffset.UtcNow;
            var cleanup = new GameViewRecordingCleanupRecord(
                GameViewRecordingCleanupRecord.CurrentSchemaVersion,
                recordingId,
                digest,
                Enum.GetValues(typeof(GameViewRecordingStateRestorationKind))
                    .Cast<GameViewRecordingStateRestorationKind>()
                    .Select(kind => new GameViewRecordingStateRestoration(
                        kind,
                        beforeValue: "unchanged",
                        afterValue: "unchanged",
                        changed: false,
                        restoreAttempted: false,
                        GameViewRecordingStateRestorationDisposition.Unchanged,
                        reasonCode: null))
                    .ToArray(),
                Enum.GetValues(typeof(GameViewRecordingResourceKind))
                    .Cast<GameViewRecordingResourceKind>()
                    .Select(kind => new GameViewRecordingResourceRelease(
                        kind,
                        acquired: false,
                        releaseAttempted: false,
                        GameViewRecordingResourceReleaseDisposition.NotAcquired,
                        reasonCode: null))
                    .ToArray(),
                GameViewRecordingCleanupDisposition.Complete,
                completedAtUtc);
            var snapshot = new GameViewRecordingSnapshot(
                recordingId,
                digest,
                EffectiveMaxDurationSeconds: 120,
                State: GameViewRecordingState.Completed,
                StopReason: GameViewRecordingStopReason.Manual,
                Failure: GameViewRecordingFailure.None,
                Runtime: new GameViewRecordingRuntimeIdentity(
                    Guid.NewGuid(),
                    "Windows",
                    "Unity Media Encoder",
                    "6000.3.11f1"),
                Cleanup: cleanup,
                Target: new GameViewRecordingTargetObservation(
                    "playModeView:1",
                    "gameView:1",
                    display: 0,
                    new PixelDimensions(320, 240),
                    new PixelDimensions(320, 240),
                    "upright",
                    UnityProjectColorSpace.Linear),
                Timing: new GameViewRecordingTimingObservation(
                    monotonicStartedTimestamp: 1,
                    monotonicStopRequestedTimestamp: 2,
                    monotonicCompletedTimestamp: 3,
                    monotonicFrequency: 1_000,
                    gameTimeStartedSeconds: 0,
                    gameTimeCompletedSeconds: 1,
                    timeScaleStarted: 1,
                    timeScaleCompleted: 1,
                    frameCountStarted: 0,
                    frameCountCompleted: 30,
                    mp4DurationSeconds: null,
                    encodedFrameCount: null,
                    effectiveFrameRate: null,
                    droppedFrameCount: null,
                    duplicatedFrameCount: null,
                    delayedFrameCount: null),
                StartedAtUtc: completedAtUtc.AddSeconds(-1),
                StopRequestedAtUtc: completedAtUtc.AddMilliseconds(-100),
                CompletedAtUtc: completedAtUtc,
                UpdatedAtUtc: completedAtUtc,
                Message: "completed",
                StartBinding: new IpcGameViewRecordingStartBinding(
                    new ProcessIdentity(1, 1),
                    new GameViewRecordingRuntimeIdentity(
                        Guid.NewGuid(),
                        "Windows",
                        "Unity Media Encoder",
                        "6000.3.11f1"),
                    new UnityEditorGenerationSnapshot(1, 1, 1, 1)));

            GameViewRecordingSessionSnapshotStore.Save(snapshot);

            var restored = GameViewRecordingSessionSnapshotStore.TryLoad();
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.RecordingId, Is.EqualTo(recordingId));
            Assert.That(restored.RequestDigest, Is.EqualTo(digest));
            Assert.That(restored.State, Is.EqualTo(GameViewRecordingState.Completed));
            Assert.That(restored.Runtime, Is.EqualTo(snapshot.Runtime));
            Assert.That(restored.StartBinding, Is.EqualTo(snapshot.StartBinding));
            Assert.That(restored.Target, Is.EqualTo(snapshot.Target));
            Assert.That(restored.Timing, Is.EqualTo(snapshot.Timing));
            Assert.That(restored.Cleanup.Disposition, Is.EqualTo(GameViewRecordingCleanupDisposition.Complete));
            Assert.That(
                restored.Cleanup.StateRestorations.Select(item => item.Kind),
                Is.EquivalentTo(cleanup.StateRestorations.Select(item => item.Kind)));
            Assert.That(
                restored.Cleanup.ResourceReleases.Select(item => item.Kind),
                Is.EquivalentTo(cleanup.ResourceReleases.Select(item => item.Kind)));
        }
    }
}
