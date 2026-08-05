using NUnit.Framework;
using ContractCaptureProfile = MackySoft.Ucli.Contracts.Recording.GameViewRecordingCaptureProfile;
using ContractCodec = MackySoft.Ucli.Contracts.Recording.GameViewRecordingCodec;
using ContractContainer = MackySoft.Ucli.Contracts.Recording.GameViewRecordingContainer;
using ContractTimingMode = MackySoft.Ucli.Contracts.Recording.GameViewRecordingTimingMode;
using GameViewRecorderCompatibilityMetadata = MackySoft.Ucli.Contracts.Recording.GameViewRecorderCompatibilityMetadata;

namespace MackySoft.Ucli.Unity.Recording.Recorder
{
    [TestFixture]
    internal sealed class UnityRecorderAdapterTests
    {
        [Test]
        public void LoadedRecorderAssembly_ExposesVerifiedAdapterMetadata ()
        {
            Assert.That(GameViewRecordingAdapterRegistry.Shared.TryGet(out var adapter), Is.True);
            Assert.That(adapter.Metadata.AdapterId, Is.EqualTo(GameViewRecorderCompatibilityMetadata.AdapterId));
            Assert.That(adapter.Metadata.AdapterVersion, Is.EqualTo(GameViewRecorderCompatibilityMetadata.AdapterVersion));
            Assert.That(adapter.Metadata.RecorderPackageId, Is.EqualTo(GameViewRecorderCompatibilityMetadata.PackageId));
            Assert.That(
                adapter.Metadata.RecorderPackageVersionRange,
                Is.EqualTo(GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange));
            Assert.That(adapter.Metadata.UnityVersionRange, Is.EqualTo("[6000.3.11f1,6000.3.12)"));
            Assert.That(
                adapter.Metadata.SupportedPlatforms,
                Is.EqualTo(GameViewRecordingEditorPlatform.Windows));
            Assert.That(adapter.Metadata.Limits.DimensionMultiple, Is.EqualTo(2));
            Assert.That(adapter.Metadata.CaptureProfile, Is.EqualTo(
                new ContractCaptureProfile(
                    ContractContainer.Mp4,
                    ContractCodec.H264,
                    audio: false,
                    alpha: false,
                    encodingProfile: "coreEncoder",
                    encodingQuality: "high",
                    timingMode: ContractTimingMode.ConstantFrameRateCapture)));
        }
    }
}
