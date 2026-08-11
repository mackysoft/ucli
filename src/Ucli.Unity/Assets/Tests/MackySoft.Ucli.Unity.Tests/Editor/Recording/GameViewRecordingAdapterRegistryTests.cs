using System;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;
using NUnit.Framework;
using GameViewRecorderCompatibilityMetadata = MackySoft.Ucli.Contracts.Recording.GameViewRecorderCompatibilityMetadata;
using ContractCaptureProfile = MackySoft.Ucli.Contracts.Recording.GameViewRecordingCaptureProfile;
using ContractCodec = MackySoft.Ucli.Contracts.Recording.GameViewRecordingCodec;
using ContractContainer = MackySoft.Ucli.Contracts.Recording.GameViewRecordingContainer;
using ContractLimits = MackySoft.Ucli.Contracts.Recording.GameViewRecordingLimits;
using ContractTimingMode = MackySoft.Ucli.Contracts.Recording.GameViewRecordingTimingMode;

namespace MackySoft.Ucli.Unity.Recording
{
    [TestFixture]
    internal sealed class GameViewRecordingAdapterRegistryTests
    {
        [Test]
        public void TryRegister_RetainsExactlyOneProductAdapter ()
        {
            var registry = new GameViewRecordingAdapterRegistry();
            var registered = new StubAdapter("registered");
            var competing = new StubAdapter("competing");

            Assert.That(registry.TryRegister(registered, out var firstError), Is.True);
            Assert.That(firstError, Is.Null);
            Assert.That(registry.TryRegister(registered, out var repeatedError), Is.True);
            Assert.That(repeatedError, Is.Null);
            Assert.That(registry.TryRegister(competing, out var conflictError), Is.False);
            Assert.That(conflictError, Does.Contain("registered"));
            Assert.That(registry.TryGet(out var observed), Is.True);
            Assert.That(observed, Is.SameAs(registered));
        }

        [Test]
        public void TryGetStopIntent_AfterDispatchDeadline_RetainsThenExpiresTheCompletedIntent ()
        {
            var registry = new GameViewRecordingAdapterRegistry();
            var recordingId = Guid.NewGuid();
            var requestedAtUtc = DateTimeOffset.UtcNow;
            var dispatchDeadlineUtc = requestedAtUtc.AddMinutes(1);
            var requestDigest = Sha256Digest.Parse(new string('a', 64));
            var binding = CreateStartBinding();

            Assert.That(registry.TryRegisterStopIntent(
                recordingId,
                requestDigest,
                effectiveMaxDurationSeconds: 120,
                binding,
                dispatchDeadlineUtc,
                requestedAtUtc,
                out _), Is.True);
            Assert.That(registry.TryObserveStopBeforeStart(
                recordingId,
                requestDigest,
                effectiveMaxDurationSeconds: 120,
                binding,
                out var observedIntent), Is.True);
            Assert.That(observedIntent.StartObserved, Is.True);

            registry.RemoveExpiredStopIntents(dispatchDeadlineUtc);

            Assert.That(registry.TryGetStopIntent(recordingId, out var retained), Is.True);
            Assert.That(retained.StartObserved, Is.True);

            registry.RemoveExpiredStopIntents(dispatchDeadlineUtc.AddDays(1));

            Assert.That(registry.TryGetStopIntent(recordingId, out _), Is.False);
        }

        [Test]
        public void TryRegisterStopIntent_WhenRecordingIdentityConflicts_RetainsTheFirstIntent ()
        {
            var registry = new GameViewRecordingAdapterRegistry();
            var recordingId = Guid.NewGuid();
            var binding = CreateStartBinding();
            var deadlineUtc = DateTimeOffset.UtcNow.AddMinutes(1);
            var firstDigest = Sha256Digest.Parse(new string('a', 64));

            Assert.That(registry.TryRegisterStopIntent(
                recordingId,
                firstDigest,
                effectiveMaxDurationSeconds: 120,
                binding,
                deadlineUtc,
                DateTimeOffset.UtcNow,
                out _), Is.True);
            Assert.That(registry.TryRegisterStopIntent(
                recordingId,
                Sha256Digest.Parse(new string('b', 64)),
                effectiveMaxDurationSeconds: 120,
                binding,
                deadlineUtc,
                DateTimeOffset.UtcNow,
                out _), Is.False);
            Assert.That(registry.TryGetStopIntent(recordingId, out var intent), Is.True);
            Assert.That(intent.RequestDigest, Is.EqualTo(firstDigest));
        }

        private static IpcGameViewRecordingStartBinding CreateStartBinding ()
        {
            return new IpcGameViewRecordingStartBinding(
                new ProcessIdentity(1, 1),
                new GameViewRecordingRuntimeIdentity(
                    Guid.NewGuid(),
                    "Windows",
                    "Unity Media Encoder",
                    "6000.3.11f1"),
                new UnityEditorGenerationSnapshot(1, 1, 1, 1));
        }

        private sealed class StubAdapter : IGameViewRecordingAdapter
        {
            public StubAdapter (string adapterId)
            {
                Metadata = new GameViewRecordingAdapterMetadata(
                    adapterId,
                    "1",
                    GameViewRecorderCompatibilityMetadata.PackageId,
                    GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
                    "[6000.3.11f1,6000.3.12)",
                    GameViewRecordingEditorPlatform.Windows,
                    new ContractCaptureProfile(
                        ContractContainer.Mp4,
                        ContractCodec.H264,
                        audio: false,
                        alpha: false,
                        encodingProfile: "coreEncoder",
                        encodingQuality: "high",
                        timingMode: ContractTimingMode.ConstantFrameRateCapture),
                    new ContractLimits(
                        minimumWidth: 10,
                        maximumWidth: 4096,
                        minimumHeight: 10,
                        maximumHeight: 4096,
                        dimensionMultiple: 2,
                        minimumFrameRate: 1,
                        maximumFrameRate: 120,
                        defaultMaxDurationSeconds: 120,
                        maximumMaxDurationSeconds: 600));
            }

            public GameViewRecordingAdapterMetadata Metadata { get; }

            public event Action<GameViewRecordingSnapshot> StateChanged
            {
                add { }
                remove { }
            }

            public GameViewRecordingRuntimeAdmission GetRuntimeAdmission ()
            {
                throw new NotSupportedException();
            }

            public GameViewRecordingOperationResult Start (GameViewRecordingStartRequest request)
            {
                throw new NotSupportedException();
            }

            public GameViewRecordingOperationResult GetStatus (Guid? recordingId)
            {
                throw new NotSupportedException();
            }

            public GameViewRecordingOperationResult Stop (Guid recordingId)
            {
                throw new NotSupportedException();
            }
        }
    }
}
