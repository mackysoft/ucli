using MackySoft.Ucli.Application.Features.Recording.UseCases;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Hosting.Cli.Recording;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Recording;

public sealed class GameViewRecordingCommandResultFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task StartSuccess_PreservesExecutionPayloadDiscriminator ()
    {
        var commandResult = GameViewRecordingCommandResultFactory.CreateStart(
            GameViewRecordingServiceResult<GameViewRecordingExecutionPayload>.Success(
                CreateActivePayload()));

        var output = await WriteAsync(commandResult);

        using var json = StdoutJsonParser.ParseSinglePrettyPrintedObject(output.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            json.RootElement,
            UcliCommandNames.RecordingStart);
        JsonAssert.For(json.RootElement.GetProperty("payload"))
            .HasString("payloadKind", "active")
            .HasProperty("executionRef", execution => execution
                .HasString("id", RecordingId.ToString("D")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartFailure_WithExecutionCheckpoint_PreservesExecutionPayload ()
    {
        var commandResult = GameViewRecordingCommandResultFactory.CreateStart(
            GameViewRecordingServiceResult<GameViewRecordingExecutionPayload>.Failure(
                ExecutionError.Timeout("Monitoring stopped."),
                CreateActivePayload()));

        var output = await WriteAsync(commandResult);

        using var json = StdoutJsonParser.ParseSinglePrettyPrintedObject(output.StdOut);
        var payload = json.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "detailed")
            .HasProperty("execution", execution => execution
                .HasString("payloadKind", "active")
                .HasProperty("executionRef", executionRef => executionRef
                    .HasString("id", RecordingId.ToString("D"))));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartCancellation_WithExecutionCheckpoint_UsesTheStandardCanceledOutcome ()
    {
        var commandResult = GameViewRecordingCommandResultFactory.CreateStart(
            GameViewRecordingServiceResult<GameViewRecordingExecutionPayload>.Failure(
                ExecutionError.Canceled("Caller stopped waiting."),
                CreateActivePayload()));

        var output = await WriteAsync(commandResult);

        Assert.Equal((int)CliExitCode.ToolError, commandResult.ExitCode);
        Assert.Equal(CommandResult.Canceled(UcliCommandNames.RecordingStart, "Canceled.").ExitCode, commandResult.ExitCode);
        using var json = StdoutJsonParser.ParseSinglePrettyPrintedObject(output.StdOut);
        CommandResultAssert.HasSingleError(json.RootElement, ExecutionErrorCodes.Canceled);
        JsonAssert.For(json.RootElement.GetProperty("payload"))
            .HasString("payloadKind", "detailed")
            .HasProperty("execution", execution => execution
                .HasProperty("executionRef", executionRef => executionRef
                    .HasString("id", RecordingId.ToString("D"))));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StopSuccess_WithRecoveryPayload_PreservesExecutionPayloadDiscriminator ()
    {
        var commandResult = GameViewRecordingCommandResultFactory.CreateStop(
            GameViewRecordingServiceResult<GameViewRecordingStopResultPayload>.Success(
                CreateRecoveryPayload()));

        var output = await WriteAsync(commandResult);

        using var json = StdoutJsonParser.ParseSinglePrettyPrintedObject(output.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            json.RootElement,
            UcliCommandNames.RecordingStop);
        JsonAssert.For(json.RootElement.GetProperty("payload"))
            .HasString("payloadKind", "recovery")
            .HasProperty("executionRef", executionRef => executionRef
                .HasString("lifecycle", "recovery"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StatusSuccess_EmitsTheClosedStatusPayload ()
    {
        var execution = CreateActivePayload();
        var commandResult = GameViewRecordingCommandResultFactory.CreateStatus(
            GameViewRecordingServiceResult<GameViewRecordingStatusPayload>.Success(
                new GameViewRecordingStatusPayload(
                    execution.Project,
                    CreateMissingCapability(),
                    new SelectedGameViewRecordingSelection(execution))));

        var output = await WriteAsync(commandResult);

        using var json = StdoutJsonParser.ParseSinglePrettyPrintedObject(output.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            json.RootElement,
            UcliCommandNames.RecordingStatus);
        var payload = json.RootElement.GetProperty("payload");
        Assert.Equal(
            ["capability", "payloadKind", "project", "recordingSelection"],
            payload
                .EnumerateObject()
                .Select(static property => property.Name)
                .Order());
        Assert.Equal("status", payload.GetProperty("payloadKind").GetString());
    }

    private static readonly Guid RecordingId =
        Guid.Parse("2b5b01d2-00ed-40a1-b956-69de49a47629");

    private static async Task<CommandExecutionResult> WriteAsync (CommandResult result)
    {
        var writer = CommandResultTestWriter.Create();
        return await CommandResultCapture.ExecuteSynchronousCommandAsync(() =>
        {
            writer.WriteToStandardOutput(result);
            return result.ExitCode;
        });
    }

    private static GameViewRecordingActivePayload CreateActivePayload ()
    {
        var digest = Sha256Digest.Compute([1, 2, 3]);
        var createdAtUtc = new DateTimeOffset(2026, 8, 5, 1, 0, 0, TimeSpan.Zero);
        var requestRef = new PathArtifactRef(
            GameViewRecordingArtifactKinds.Request,
            GameViewRecordingArtifactMediaTypes.Json,
            new ArtifactPath("recordings/request.json"),
            digest,
            sizeBytes: 10,
            createdAtUtc);
        var progress = new GameViewRecordingActiveProgress(
            GameViewRecordingState.Recording,
            effectiveMaxDurationSeconds: 120,
            encodedFrameCount: 10,
            startedAtUtc: createdAtUtc,
            stopRequestedAtUtc: null,
            updatedAtUtc: createdAtUtc.AddSeconds(1));
        return new GameViewRecordingActivePayload(
            new UnityProjectIdentity(
                "C:/repo",
                new ProjectFingerprint(new string('a', 64)),
                "6000.0.0f1"),
            new ActiveExecutionRef(
                GameViewRecordingExecutionContract.Kind,
                RecordingId,
                digest,
                GameViewRecordingExecutionContract.ToExecutionState(progress.State),
                new ExecutionStatusLocator("recordings/active")),
            digest,
            requestRef,
            progress,
            [requestRef],
            Array.Empty<GameViewRecordingDiagnostic>());
    }

    private static GameViewRecordingRecoveryPayload CreateRecoveryPayload ()
    {
        var active = CreateActivePayload();
        var updatedAtUtc = active.Progress.UpdatedAtUtc.AddSeconds(1);
        var progress = new GameViewRecordingRecoveryProgress(
            GameViewRecordingState.Finalizing,
            active.Progress.EffectiveMaxDurationSeconds,
            active.Progress.EncodedFrameCount,
            active.Progress.StartedAtUtc,
            stopRequestedAtUtc: updatedAtUtc,
            updatedAtUtc);
        return new GameViewRecordingRecoveryPayload(
            active.Project,
            new RecoveryExecutionRef(
                active.ExecutionRef.Kind,
                active.ExecutionRef.Id,
                active.ExecutionRef.DefinitionDigest,
                GameViewRecordingExecutionContract.ToExecutionState(progress.State),
                active.ExecutionRef.StatusLocator),
            active.RequestDigest,
            active.RequestRef,
            progress,
            active.ArtifactRefs,
            active.Diagnostics);
    }

    private static GameViewRecordingCapability CreateMissingCapability () =>
        new(
            new GameViewRecordingPackageCapability(
                GameViewRecordingPackageState.Missing,
                GameViewRecorderCompatibilityMetadata.PackageId,
                version: null),
            new GameViewRecordingCompatibilityCapability(
                GameViewRecordingCompatibilityState.NotApplicable,
                GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
                resolvedVersion: null),
            new GameViewRecordingAdapterCapability(
                GameViewRecordingAdapterState.NotApplicable,
                adapterId: null,
                adapterVersion: null),
            new GameViewRecordingRuntimeAdmission(
                GameViewRecordingRuntimeAdmissionState.Unobserved,
                [GameViewRecordingErrorCodes.Unavailable]),
            limits: null,
            captureProfile: null);
}
