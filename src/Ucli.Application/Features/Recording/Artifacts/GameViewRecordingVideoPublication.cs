namespace MackySoft.Ucli.Application.Features.Recording.Artifacts;

/// <summary> Contains the immutable video reference and independently parsed MP4 observations. </summary>
internal sealed record GameViewRecordingVideoPublication
{
    public GameViewRecordingVideoPublication (
        PathArtifactRef artifact,
        string codec,
        ulong encodedFrameCount,
        double durationSeconds,
        double effectiveFrameRate)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(codec);
        if (encodedFrameCount == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(encodedFrameCount),
                encodedFrameCount,
                "Encoded frame count must be positive.");
        }
        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationSeconds),
                durationSeconds,
                "Video duration must be positive and finite.");
        }
        if (!double.IsFinite(effectiveFrameRate) || effectiveFrameRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectiveFrameRate),
                effectiveFrameRate,
                "Effective frame rate must be positive and finite.");
        }

        Artifact = artifact;
        Codec = codec;
        EncodedFrameCount = encodedFrameCount;
        DurationSeconds = durationSeconds;
        EffectiveFrameRate = effectiveFrameRate;
    }

    public PathArtifactRef Artifact { get; }

    public string Codec { get; }

    public ulong EncodedFrameCount { get; }

    public double DurationSeconds { get; }

    public double EffectiveFrameRate { get; }
}
