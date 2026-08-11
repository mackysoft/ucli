using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Contracts.Tests.Recording;

public sealed class GameViewRecordingPayloadContractTests
{
    public static TheoryData<GameViewRecordingPayload, string, Type> PayloadBranches => new()
    {
        {
            GameViewRecordingContractTestFactory.CreateActivePayload(),
            "active",
            typeof(GameViewRecordingActivePayload)
        },
        {
            GameViewRecordingContractTestFactory.CreateRecoveryPayload(),
            "recovery",
            typeof(GameViewRecordingRecoveryPayload)
        },
        {
            GameViewRecordingContractTestFactory.CreateTerminalPayload(),
            "terminal",
            typeof(GameViewRecordingTerminalPayload)
        },
        {
            new GameViewRecordingStatusPayload(
                GameViewRecordingContractTestFactory.CreateProject(),
                GameViewRecordingContractTestFactory.CreateMissingCapability(),
                new SelectedGameViewRecordingSelection(
                    GameViewRecordingContractTestFactory.CreateActivePayload())),
            "status",
            typeof(GameViewRecordingStatusPayload)
        },
    };

    [Theory]
    [MemberData(nameof(PayloadBranches))]
    [Trait("Size", "Small")]
    public void PublicPayloadBranches_RoundTripWithTheirClosedDiscriminators (
        GameViewRecordingPayload payload,
        string expectedPayloadKind,
        Type expectedType)
    {
        var json = JsonSerializer.Serialize<GameViewRecordingPayload>(
            payload,
            IpcJsonSerializerOptions.StrictPropertyNames);
        var parsed = JsonNode.Parse(json)!.AsObject();
        var roundTripped = JsonSerializer.Deserialize<GameViewRecordingPayload>(
            json,
            IpcJsonSerializerOptions.StrictPropertyNames);

        Assert.Equal(expectedPayloadKind, parsed["payloadKind"]!.GetValue<string>());
        Assert.IsType(expectedType, roundTripped);
        if (payload is GameViewRecordingExecutionPayload execution)
        {
            Assert.Equal(
                TextVocabulary.GetText(execution.ExecutionReference.Lifecycle),
                parsed["executionRef"]!["lifecycle"]!.GetValue<string>());
        }
        if (roundTripped is GameViewRecordingStatusPayload status)
        {
            var selection = Assert.IsType<SelectedGameViewRecordingSelection>(status.RecordingSelection);
            Assert.IsType<GameViewRecordingActivePayload>(selection.Recording);
            Assert.Equal(
                "active",
                parsed["recordingSelection"]!["recording"]!["executionRef"]!["lifecycle"]!
                    .GetValue<string>());
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PublicPayload_WhenUnknownPropertyIsPresent_RejectsJson ()
    {
        var payload = GameViewRecordingContractTestFactory.CreateActivePayload();
        var json = JsonSerializer.SerializeToNode<GameViewRecordingPayload>(
            payload,
            IpcJsonSerializerOptions.StrictPropertyNames)!.AsObject();
        json["unknown"] = true;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GameViewRecordingPayload>(
            json,
            IpcJsonSerializerOptions.StrictPropertyNames));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ActiveProgress_WhenStateMapsToAnotherLifecycle_RejectsValue ()
    {
        Assert.Throws<ArgumentException>(() => new GameViewRecordingActiveProgress(
            GameViewRecordingState.Finalizing,
            effectiveMaxDurationSeconds: 120,
            encodedFrameCount: 10,
            startedAtUtc: GameViewRecordingContractTestFactory.StartedAtUtc,
            stopRequestedAtUtc: GameViewRecordingContractTestFactory.StartedAtUtc.AddSeconds(1),
            updatedAtUtc: GameViewRecordingContractTestFactory.CompletedAtUtc));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ActivePayload_WhenReferenceAndProgressStatesDiffer_RejectsValue ()
    {
        var valid = GameViewRecordingContractTestFactory.CreateActivePayload();
        var mismatchedReference = new ActiveExecutionRef(
            valid.ExecutionRef.Kind,
            valid.ExecutionRef.Id,
            valid.ExecutionRef.DefinitionDigest,
            GameViewRecordingExecutionContract.Preparing,
            valid.ExecutionRef.StatusLocator);

        Assert.Throws<ArgumentException>(() =>
        {
            _ = new GameViewRecordingActivePayload(
                valid.Project,
                mismatchedReference,
                valid.RequestDigest,
                valid.RequestRef,
                valid.Progress,
                valid.ArtifactRefs,
                valid.Diagnostics);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ActiveExecutionReference_WhenStatusLocatorIsMissing_RejectsValue ()
    {
        var valid = GameViewRecordingContractTestFactory.CreateActivePayload();
        Assert.Throws<ArgumentNullException>(() => new ActiveExecutionRef(
            valid.ExecutionRef.Kind,
            valid.ExecutionRef.Id,
            valid.ExecutionRef.DefinitionDigest,
            valid.ExecutionRef.State,
            statusLocator: null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TerminalPayload_WhenSummaryAndProgressStatesDiffer_RejectsValue ()
    {
        var valid = GameViewRecordingContractTestFactory.CreateTerminalPayload();
        var mismatchedSummary = new GameViewRecordingTerminalSummary(
            GameViewRecordingState.Failed,
            GameViewRecordingStopReason.InternalFailure,
            GameViewRecordingVideoDisposition.Missing,
            GameViewRecordingCleanupDisposition.Failed,
            GameViewRecordingContractTestFactory.StartedAtUtc,
            GameViewRecordingContractTestFactory.CompletedAtUtc);

        Assert.Throws<ArgumentException>(() => new GameViewRecordingTerminalPayload(
            valid.Project,
            valid.ExecutionRef,
            valid.RequestDigest,
            valid.RequestRef,
            valid.Progress,
            valid.ArtifactRefs,
            valid.Diagnostics,
            mismatchedSummary));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Diagnostic_WhenSeverityIsUndefined_RejectsValue ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameViewRecordingDiagnostic(
            GameViewRecordingErrorCodes.Interrupted,
            (GameViewRecordingDiagnosticSeverity)0,
            "Interrupted.",
            Array.Empty<ArtifactRef>()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndeterminateTerminalSummary_WithoutAcceptedStart_RoundTripsNullStartTime ()
    {
        var summary = new GameViewRecordingTerminalSummary(
            GameViewRecordingState.Indeterminate,
            GameViewRecordingStopReason.Unconfirmed,
            GameViewRecordingVideoDisposition.Unconfirmed,
            GameViewRecordingCleanupDisposition.Unconfirmed,
            startedAtUtc: null,
            GameViewRecordingContractTestFactory.CompletedAtUtc);

        var json = JsonSerializer.Serialize(summary, IpcJsonSerializerOptions.StrictPropertyNames);
        var roundTripped = JsonSerializer.Deserialize<GameViewRecordingTerminalSummary>(
            json,
            IpcJsonSerializerOptions.StrictPropertyNames)!;

        Assert.Null(roundTripped.StartedAtUtc);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(GameViewRecordingState.Completed)]
    [InlineData(GameViewRecordingState.Failed)]
    public void AcceptedTerminalSummary_WithoutAcceptedStart_RejectsValue (
        GameViewRecordingState state)
    {
        var videoDisposition = state == GameViewRecordingState.Completed
            ? GameViewRecordingVideoDisposition.Available
            : GameViewRecordingVideoDisposition.Missing;
        var cleanupDisposition = state == GameViewRecordingState.Completed
            ? GameViewRecordingCleanupDisposition.Complete
            : GameViewRecordingCleanupDisposition.Failed;

        Assert.Throws<ArgumentException>(() => new GameViewRecordingTerminalSummary(
            state,
            GameViewRecordingStopReason.InternalFailure,
            videoDisposition,
            cleanupDisposition,
            startedAtUtc: null,
            GameViewRecordingContractTestFactory.CompletedAtUtc));
    }
}
