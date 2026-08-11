using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Contracts.Tests.Recording;

public sealed class GameViewRecordingRequestAndCapabilityTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Request_RoundTripsTheClosedPublicShape ()
    {
        var request = new GameViewRecordingRequest(
            GameViewRecordingRequest.CurrentSchemaVersion,
            new PixelDimensions(1920, 1080),
            frameRate: 30,
            maxDurationSeconds: 120);

        var json = JsonSerializer.Serialize(request, IpcJsonSerializerOptions.StrictPropertyNames);
        var roundTripped = JsonSerializer.Deserialize<GameViewRecordingRequest>(
            json,
            IpcJsonSerializerOptions.StrictPropertyNames);

        Assert.Equal(request, roundTripped);
        Assert.Contains("\"schemaVersion\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"resolution\":{\"width\":1920,\"height\":1080}", json, StringComparison.Ordinal);
        Assert.Contains("\"frameRate\":30", json, StringComparison.Ordinal);
        Assert.Contains("\"maxDurationSeconds\":120", json, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(1920, 1080, 0, 120)]
    [InlineData(1920, 1080, 30, 0)]
    public void Request_WhenScalarInvariantIsViolated_RejectsValue (
        int width,
        int height,
        int frameRate,
        int maxDurationSeconds)
    {
        Assert.ThrowsAny<ArgumentException>(() => new GameViewRecordingRequest(
            GameViewRecordingRequest.CurrentSchemaVersion,
            new PixelDimensions(width, height),
            frameRate,
            maxDurationSeconds));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Limits_RequirePositiveDimensionMultipleAndAlignedBounds ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameViewRecordingLimits(
            minimumWidth: 2,
            maximumWidth: 3840,
            minimumHeight: 2,
            maximumHeight: 2160,
            dimensionMultiple: 0,
            minimumFrameRate: 1,
            maximumFrameRate: 60,
            defaultMaxDurationSeconds: 120,
            maximumMaxDurationSeconds: 600));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameViewRecordingLimits(
            minimumWidth: 3,
            maximumWidth: 3840,
            minimumHeight: 2,
            maximumHeight: 2160,
            dimensionMultiple: 2,
            minimumFrameRate: 1,
            maximumFrameRate: 60,
            defaultMaxDurationSeconds: 120,
            maximumMaxDurationSeconds: 600));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Capability_WhenAdapterIsRegistered_RequiresLimitsAndCaptureProfile ()
    {
        var package = new GameViewRecordingPackageCapability(
            GameViewRecordingPackageState.Resolved,
            GameViewRecorderCompatibilityMetadata.PackageId,
            "5.1.5");
        var compatibility = new GameViewRecordingCompatibilityCapability(
            GameViewRecordingCompatibilityState.Supported,
            GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
            "5.1.5");
        var adapter = new GameViewRecordingAdapterCapability(
            GameViewRecordingAdapterState.Registered,
            GameViewRecorderCompatibilityMetadata.AdapterId,
            GameViewRecorderCompatibilityMetadata.AdapterVersion);
        var runtimeAdmission = new GameViewRecordingRuntimeAdmission(
            GameViewRecordingRuntimeAdmissionState.Ready,
            Array.Empty<UcliCode>());

        Assert.Throws<ArgumentException>(() => new GameViewRecordingCapability(
            package,
            compatibility,
            adapter,
            runtimeAdmission,
            limits: null,
            captureProfile: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompatibilityMetadata_ExposesTheDistributionContract ()
    {
        Assert.Equal("com.unity.recorder", GameViewRecorderCompatibilityMetadata.PackageId);
        Assert.Equal("[5.1.5,5.2.0)", GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange);
        Assert.Equal("com.mackysoft.ucli.game-view-recorder", GameViewRecorderCompatibilityMetadata.AdapterId);
        Assert.Equal("1", GameViewRecorderCompatibilityMetadata.AdapterVersion);
    }
}
