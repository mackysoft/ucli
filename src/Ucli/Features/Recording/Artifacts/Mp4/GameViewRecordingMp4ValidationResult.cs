using MackySoft.Ucli.Contracts.Presentation;

namespace MackySoft.Ucli.Features.Recording.Artifacts.Mp4;

/// <summary> Describes the validated video timing and format of one finalized GameView recording. </summary>
internal sealed record GameViewRecordingMp4ValidationResult
{
    public GameViewRecordingMp4ValidationResult (
        string codec,
        PixelDimensions dimensions,
        uint movieTimescale,
        ulong movieDuration,
        uint mediaTimescale,
        uint sampleDelta,
        ulong sampleCount,
        ulong durationInMediaTimeUnits,
        double durationSeconds,
        double effectiveFrameRate)
    {
        Codec = codec;
        Dimensions = dimensions ?? throw new ArgumentNullException(nameof(dimensions));
        MovieTimescale = movieTimescale;
        MovieDuration = movieDuration;
        MediaTimescale = mediaTimescale;
        SampleDelta = sampleDelta;
        SampleCount = sampleCount;
        DurationInMediaTimeUnits = durationInMediaTimeUnits;
        DurationSeconds = durationSeconds;
        EffectiveFrameRate = effectiveFrameRate;
    }

    public string Codec { get; }

    public PixelDimensions Dimensions { get; }

    public uint MovieTimescale { get; }

    public ulong MovieDuration { get; }

    public uint MediaTimescale { get; }

    public uint SampleDelta { get; }

    public ulong SampleCount { get; }

    public ulong DurationInMediaTimeUnits { get; }

    public double DurationSeconds { get; }

    public double EffectiveFrameRate { get; }
}
