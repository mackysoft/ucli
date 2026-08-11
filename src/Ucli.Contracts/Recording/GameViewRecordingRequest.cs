using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Presentation;

namespace MackySoft.Ucli.Contracts.Recording;

/// <summary>Represents the closed, normalized version-one GameView recording request.</summary>
[Title("GameView recording request")]
[Description("A versioned request to record the current GameView as an MP4 artifact.")]
public sealed record GameViewRecordingRequest
{
    /// <summary>Gets the only supported request schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Initializes a structurally valid recording request.</summary>
    [JsonConstructor]
    public GameViewRecordingRequest (
        int schemaVersion,
        PixelDimensions resolution,
        int frameRate,
        int maxDurationSeconds)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                $"Recording request schema version must be {CurrentSchemaVersion}.");
        }

        SchemaVersion = schemaVersion;
        Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        FrameRate = ContractArgumentGuard.RequirePositive(frameRate, nameof(frameRate));
        MaxDurationSeconds = ContractArgumentGuard.RequirePositive(maxDurationSeconds, nameof(maxDurationSeconds));
    }

    [JsonInclude]
    [JsonRequired]
    [UcliInt32Constant(CurrentSchemaVersion)]
    [Description("The GameView recording request schema version. The only supported value is 1.")]
    public int SchemaVersion { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("The exact GameView render and video dimensions. Both values must satisfy the current recording capability.")]
    public PixelDimensions Resolution { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [UcliInt32Minimum(1)]
    [Description("The requested constant video frame rate, within the current recording capability.")]
    public int FrameRate { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [UcliInt32Minimum(1)]
    [Description("The effective per-recording duration limit in seconds, within the admitted recording capability.")]
    public int MaxDurationSeconds { get; private init; }
}
