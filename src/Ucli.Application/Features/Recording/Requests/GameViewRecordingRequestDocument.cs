using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Features.Recording.Requests;

/// <summary>Contains the validated caller-provided recording request before capability defaults are applied.</summary>
[Title("GameView recording request")]
[Description("A versioned request to record the current GameView as an MP4 artifact.")]
internal sealed record GameViewRecordingRequestDocument
{
    [JsonConstructor]
    public GameViewRecordingRequestDocument (
        int schemaVersion,
        PixelDimensions resolution,
        int frameRate,
        UcliOptionalInt32 maxDurationSeconds = default)
    {
        if (schemaVersion != GameViewRecordingRequest.CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                $"Recording request schema version must be {GameViewRecordingRequest.CurrentSchemaVersion}.");
        }
        if (frameRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameRate), frameRate, "Recording frame rate must be positive.");
        }
        if (maxDurationSeconds.HasValue && maxDurationSeconds.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDurationSeconds),
                maxDurationSeconds.Value,
                "Recording duration must be positive when supplied.");
        }

        SchemaVersion = schemaVersion;
        Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        FrameRate = frameRate;
        MaxDurationSeconds = maxDurationSeconds;
    }

    [JsonInclude]
    [JsonRequired]
    [UcliInt32Constant(GameViewRecordingRequest.CurrentSchemaVersion)]
    [Description("The GameView recording request schema version. The only supported value is 1.")]
    public int SchemaVersion { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public PixelDimensions Resolution { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [UcliInt32Minimum(1)]
    [Description("The requested constant video frame rate, within the current recording capability.")]
    public int FrameRate { get; private init; }

    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [UcliInt32Minimum(1)]
    [Description("An optional per-recording duration limit in seconds, within the current recording capability.")]
    public UcliOptionalInt32 MaxDurationSeconds { get; private init; }
}

/// <summary>Contains the effective request fixed before a recording starts.</summary>
internal sealed record GameViewRecordingEffectiveRequest
{
    public GameViewRecordingEffectiveRequest (
        int schemaVersion,
        PixelDimensions resolution,
        int frameRate,
        int maxDurationSeconds,
        string canonicalJson,
        Sha256Digest digest)
    {
        SchemaVersion = schemaVersion;
        Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        FrameRate = frameRate;
        MaxDurationSeconds = maxDurationSeconds;
        CanonicalJson = canonicalJson;
        Digest = digest;
    }

    public int SchemaVersion { get; }

    public PixelDimensions Resolution { get; }

    public int FrameRate { get; }

    public int MaxDurationSeconds { get; }

    public string CanonicalJson { get; }

    public Sha256Digest Digest { get; }
}
