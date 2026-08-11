using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Contracts.Tests.Recording;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class IpcGameViewRecordingContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void RecordingIpcContracts_RoundTripCorrelationAndTaggedPayloads ()
    {
        var startRequest = new IpcGameViewRecordingStartRequest(
            GameViewRecordingContractTestFactory.RecordingId,
            GameViewRecordingContractTestFactory.RequestDigest,
            GameViewRecordingContractTestFactory.CreateEffectiveRequest(),
            GameViewRecordingContractTestFactory.CreateStartBinding(),
            GameViewRecordingContractTestFactory.DispatchDeadlineUtc);
        var startResponse = new IpcGameViewRecordingStartResponse(
            GameViewRecordingContractTestFactory.CreateRuntimeSnapshot(GameViewRecordingState.Recording));
        var knownRecording = GameViewRecordingContractTestFactory.CreateRuntimeSnapshot(
            GameViewRecordingState.Recording);
        var statusRequest = new IpcGameViewRecordingStatusRequest(
            GameViewRecordingContractTestFactory.RecordingId,
            GameViewRecordingContractTestFactory.RequestDigest,
            effectiveMaxDurationSeconds: 120,
            GameViewRecordingContractTestFactory.CreateStartBinding(),
            GameViewRecordingContractTestFactory.DispatchDeadlineUtc,
            knownRecording);
        var statusResponse = new IpcGameViewRecordingStatusResponse(
            new IpcSelectedGameViewRecordingSelection(
                GameViewRecordingContractTestFactory.CreateRuntimeSnapshot(GameViewRecordingState.Finalizing)));
        var stopRequest = new IpcGameViewRecordingStopRequest(
            GameViewRecordingContractTestFactory.RecordingId,
            GameViewRecordingContractTestFactory.RequestDigest,
            effectiveMaxDurationSeconds: 120,
            GameViewRecordingContractTestFactory.CreateStartBinding(),
            GameViewRecordingContractTestFactory.DispatchDeadlineUtc,
            knownRecording);
        var stopResponse = new IpcGameViewRecordingStopResponse(
            Assert.IsAssignableFrom<IpcGameViewRecordingStopSnapshot>(
                GameViewRecordingContractTestFactory.CreateRuntimeSnapshot(
                    GameViewRecordingState.Completed)));

        Assert.Equal(GameViewRecordingContractTestFactory.RecordingId, RoundTrip(startRequest).RecordingId);
        Assert.Equal(GameViewRecordingState.Recording, RoundTrip(startResponse).Recording.State);
        Assert.Equal(GameViewRecordingContractTestFactory.RecordingId, RoundTrip(statusRequest).RecordingId);
        Assert.Equal(GameViewRecordingState.Recording, RoundTrip(statusRequest).KnownRecording!.State);
        Assert.Equal(
            GameViewRecordingContractTestFactory.RuntimeId,
            RoundTrip(statusRequest).StartBinding.Runtime.RuntimeId);
        Assert.IsType<IpcSelectedGameViewRecordingSelection>(RoundTrip(statusResponse).RecordingSelection);
        Assert.Equal(GameViewRecordingContractTestFactory.RecordingId, RoundTrip(stopRequest).RecordingId);
        Assert.Equal(
            GameViewRecordingContractTestFactory.DispatchDeadlineUtc,
            RoundTrip(stopRequest).DispatchDeadlineUtc);
        Assert.Equal(GameViewRecordingState.Recording, RoundTrip(stopRequest).KnownRecording!.State);
        Assert.Equal(GameViewRecordingState.Completed, RoundTrip(stopResponse).Recording.State);

        var startRequestJson = JsonSerializer.SerializeToNode(
            startRequest,
            IpcJsonSerializerOptions.StrictPropertyNames)!.AsObject();
        var responseJson = JsonSerializer.SerializeToNode(
            statusResponse,
            IpcJsonSerializerOptions.StrictPropertyNames)!.AsObject();
        Assert.Equal("selected", responseJson["recordingSelection"]!["kind"]!.GetValue<string>());
        Assert.False(startRequestJson.ContainsKey("stagingOutputPath"));
        Assert.False(
            responseJson["recordingSelection"]!["recording"]!
                .AsObject()
                .ContainsKey("stagingOutputPath"));
        Assert.Equal(
            42,
            startRequestJson["startBinding"]!["process"]!["processId"]!.GetValue<int>());
        Assert.Equal(
            GameViewRecordingContractTestFactory.DispatchDeadlineUtc,
            startRequestJson["dispatchDeadlineUtc"]!.GetValue<DateTimeOffset>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void StatusRequest_RejectsKnownRecordingThatCannotRepresentTheSelectedNonTerminalExecution ()
    {
        var terminal = GameViewRecordingContractTestFactory.CreateRuntimeSnapshot(
            GameViewRecordingState.Completed);
        var differentId = Guid.Parse("b217b21a-00e6-4d42-b47f-917a1d8d3fa3");

        Assert.Throws<ArgumentException>(() => new IpcGameViewRecordingStatusRequest(
            terminal.RecordingId,
            terminal.RequestDigest,
            terminal.EffectiveMaxDurationSeconds,
            GameViewRecordingContractTestFactory.CreateStartBinding(),
            GameViewRecordingContractTestFactory.DispatchDeadlineUtc,
            terminal));
        Assert.Throws<ArgumentException>(() => new IpcGameViewRecordingStatusRequest(
            differentId,
            GameViewRecordingContractTestFactory.RequestDigest,
            effectiveMaxDurationSeconds: 120,
            GameViewRecordingContractTestFactory.CreateStartBinding(),
            GameViewRecordingContractTestFactory.DispatchDeadlineUtc,
            GameViewRecordingContractTestFactory.CreateRuntimeSnapshot(
                GameViewRecordingState.Recording)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void StatusRequest_WhenKnownRecordingDiffersFromTheFixedStartFacts_RejectsValue ()
    {
        var binding = GameViewRecordingContractTestFactory.CreateStartBinding();
        var differentDigest = Sha256Digest.Compute([9, 9, 9]);
        var differentRuntime = new GameViewRecordingRuntimeIdentity(
            Guid.Parse("3c440ea6-d93d-4a2d-81eb-ae59ca0aac3f"),
            "windows",
            "media-foundation",
            "1");
        var differentGeneration = GameViewRecordingContractTestFactory.CreateGeneration(9);

        Assert.Throws<ArgumentException>(() => CreateStatusRequest(
            binding,
            CreateKnownRecording(requestDigest: differentDigest)));
        Assert.Throws<ArgumentException>(() => CreateStatusRequest(
            binding,
            CreateKnownRecording(effectiveMaxDurationSeconds: 121)));
        Assert.Throws<ArgumentException>(() => CreateStatusRequest(
            binding,
            CreateKnownRecording(runtime: differentRuntime)));
        Assert.Throws<ArgumentException>(() => CreateStatusRequest(
            binding,
            CreateKnownRecording(startGeneration: differentGeneration)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RecordingRequests_WhenDispatchDeadlineIsNotUtc_RejectValue ()
    {
        var binding = GameViewRecordingContractTestFactory.CreateStartBinding();
        var nonUtcDeadline = GameViewRecordingContractTestFactory.DispatchDeadlineUtc
            .ToOffset(TimeSpan.FromHours(1));

        Assert.Throws<ArgumentException>(() => new IpcGameViewRecordingStartRequest(
            GameViewRecordingContractTestFactory.RecordingId,
            GameViewRecordingContractTestFactory.RequestDigest,
            GameViewRecordingContractTestFactory.CreateEffectiveRequest(),
            binding,
            nonUtcDeadline));
        Assert.Throws<ArgumentException>(() => new IpcGameViewRecordingStatusRequest(
            GameViewRecordingContractTestFactory.RecordingId,
            GameViewRecordingContractTestFactory.RequestDigest,
            effectiveMaxDurationSeconds: 120,
            binding,
            nonUtcDeadline,
            knownRecording: null));
        Assert.Throws<ArgumentException>(() => new IpcGameViewRecordingStopRequest(
            GameViewRecordingContractTestFactory.RecordingId,
            GameViewRecordingContractTestFactory.RequestDigest,
            effectiveMaxDurationSeconds: 120,
            binding,
            nonUtcDeadline,
            knownRecording: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void StopRequest_WhenKnownRecordingDiffersFromTheFixedStartFacts_RejectsValue ()
    {
        var binding = GameViewRecordingContractTestFactory.CreateStartBinding();

        Assert.Throws<ArgumentException>(() => new IpcGameViewRecordingStopRequest(
            GameViewRecordingContractTestFactory.RecordingId,
            GameViewRecordingContractTestFactory.RequestDigest,
            effectiveMaxDurationSeconds: 120,
            binding,
            GameViewRecordingContractTestFactory.DispatchDeadlineUtc,
            CreateKnownRecording(effectiveMaxDurationSeconds: 121)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CapabilityIpcResponse_RoundTripsTheRegisteredRuntimeSlice ()
    {
        var response = new IpcGameViewRecordingCapabilityResponse(
            new GameViewRecordingAdapterCapability(
                GameViewRecordingAdapterState.Registered,
                GameViewRecorderCompatibilityMetadata.AdapterId,
                GameViewRecorderCompatibilityMetadata.AdapterVersion),
            new GameViewRecordingRuntimeAdmission(
                GameViewRecordingRuntimeAdmissionState.Ready,
                Array.Empty<UcliCode>()),
            new GameViewRecordingLimits(2, 3840, 2, 2160, 2, 1, 60, 120, 600),
            GameViewRecordingContractTestFactory.CreateCaptureProfile(),
            GameViewRecordingContractTestFactory.CreateStartBinding(),
            GameViewRecordingContractTestFactory.CreateRuntime());

        var roundTripped = RoundTrip(response);

        Assert.Equal(GameViewRecordingAdapterState.Registered, roundTripped.Adapter.State);
        Assert.Equal(3840, roundTripped.Limits!.MaximumWidth);
        Assert.Equal(2, roundTripped.Limits.DimensionMultiple);
        Assert.Equal(GameViewRecordingCodec.H264, roundTripped.CaptureProfile!.Codec);
        Assert.Equal(GameViewRecordingContractTestFactory.RuntimeId, roundTripped.StartBinding!.Runtime.RuntimeId);
        Assert.Equal(GameViewRecordingContractTestFactory.RuntimeId, roundTripped.ObservedRuntime!.RuntimeId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CapabilityIpcResponse_RequiresStartBindingOnlyForReadyAdmission ()
    {
        var adapter = new GameViewRecordingAdapterCapability(
            GameViewRecordingAdapterState.Registered,
            GameViewRecorderCompatibilityMetadata.AdapterId,
            GameViewRecorderCompatibilityMetadata.AdapterVersion);
        var limits = new GameViewRecordingLimits(2, 3840, 2, 2160, 2, 1, 60, 120, 600);
        var profile = GameViewRecordingContractTestFactory.CreateCaptureProfile();

        Assert.Throws<ArgumentException>(() => new IpcGameViewRecordingCapabilityResponse(
            adapter,
            new GameViewRecordingRuntimeAdmission(
                GameViewRecordingRuntimeAdmissionState.Ready,
                Array.Empty<UcliCode>()),
            limits,
            profile,
            startBinding: null));
        Assert.Throws<ArgumentException>(() => new IpcGameViewRecordingCapabilityResponse(
            adapter,
            new GameViewRecordingRuntimeAdmission(
                GameViewRecordingRuntimeAdmissionState.Blocked,
                [GameViewRecordingErrorCodes.RequiresPlayMode]),
            limits,
            profile,
            GameViewRecordingContractTestFactory.CreateStartBinding()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndeterminateSnapshot_WithoutAcceptedStart_RoundTripsNullStartTime ()
    {
        var snapshot = new IpcGameViewRecordingIndeterminateSnapshot(
            GameViewRecordingContractTestFactory.RecordingId,
            GameViewRecordingContractTestFactory.RequestDigest,
            GameViewRecordingState.Indeterminate,
            GameViewRecordingStopReason.Unconfirmed,
            failure: null,
            GameViewRecordingContractTestFactory.CreateRuntime(),
            cleanup: null,
            target: null,
            timing: null,
            effectiveMaxDurationSeconds: 120,
            encodedFrameCount: null,
            startedAtUtc: null,
            stopRequestedAtUtc: null,
            GameViewRecordingContractTestFactory.CompletedAtUtc,
            GameViewRecordingContractTestFactory.CompletedAtUtc,
            GameViewRecordingContractTestFactory.CreateGeneration(1),
            GameViewRecordingContractTestFactory.CreateGeneration(2));

        Assert.Null(RoundTrip(snapshot).StartedAtUtc);
    }

    private static T RoundTrip<T> (T value)
    {
        var json = JsonSerializer.Serialize(value, IpcJsonSerializerOptions.StrictPropertyNames);
        return JsonSerializer.Deserialize<T>(json, IpcJsonSerializerOptions.StrictPropertyNames)!;
    }

    private static IpcGameViewRecordingStatusRequest CreateStatusRequest (
        IpcGameViewRecordingStartBinding binding,
        IpcGameViewRecordingSnapshot knownRecording)
    {
        return new IpcGameViewRecordingStatusRequest(
            GameViewRecordingContractTestFactory.RecordingId,
            GameViewRecordingContractTestFactory.RequestDigest,
            effectiveMaxDurationSeconds: 120,
            binding,
            GameViewRecordingContractTestFactory.DispatchDeadlineUtc,
            knownRecording);
    }

    private static IpcGameViewRecordingSnapshot CreateKnownRecording (
        Sha256Digest? requestDigest = null,
        int effectiveMaxDurationSeconds = 120,
        GameViewRecordingRuntimeIdentity? runtime = null,
        UnityEditorGenerationSnapshot? startGeneration = null)
    {
        return new IpcGameViewRecordingActiveSnapshot(
            GameViewRecordingContractTestFactory.RecordingId,
            requestDigest ?? GameViewRecordingContractTestFactory.RequestDigest,
            GameViewRecordingState.Recording,
            runtime ?? GameViewRecordingContractTestFactory.CreateRuntime(),
            target: null,
            effectiveMaxDurationSeconds,
            encodedFrameCount: 10,
            GameViewRecordingContractTestFactory.StartedAtUtc,
            GameViewRecordingContractTestFactory.StartedAtUtc,
            startGeneration ?? GameViewRecordingContractTestFactory.CreateGeneration(1),
            GameViewRecordingContractTestFactory.CreateGeneration(2));
    }
}
