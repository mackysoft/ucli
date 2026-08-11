using MackySoft.Ucli.Features.Recording.Artifacts.Mp4;

namespace MackySoft.Ucli.Tests.Features.Recording.Artifacts.Mp4;

public sealed class GameViewRecordingMp4ValidatorTests
{
    [Theory]
    [InlineData("avc1")]
    [InlineData("avc3")]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenFinalizedMovieMatchesRequest_ReturnsVideoStructure (string codec)
    {
        var bytes = SyntheticGameViewRecordingMp4.Create(codec);
        await using var stream = new ChunkedReadStream(bytes, maximumReadSize: 3);

        var result = await new GameViewRecordingMp4Validator().ValidateAsync(
            stream,
            SyntheticGameViewRecordingMp4.Width,
            SyntheticGameViewRecordingMp4.Height,
            expectedFrameRate: 30,
            expectedMaxDurationSeconds: 120,
            CancellationToken.None);

        Assert.Equal(codec, result.Codec);
        Assert.Equal(
            new PixelDimensions(
                SyntheticGameViewRecordingMp4.Width,
                SyntheticGameViewRecordingMp4.Height),
            result.Dimensions);
        Assert.Equal(SyntheticGameViewRecordingMp4.MediaTimescale, result.MovieTimescale);
        Assert.Equal(
            (ulong)SyntheticGameViewRecordingMp4.SampleCount * SyntheticGameViewRecordingMp4.SampleDelta,
            result.MovieDuration);
        Assert.Equal(SyntheticGameViewRecordingMp4.MediaTimescale, result.MediaTimescale);
        Assert.Equal(SyntheticGameViewRecordingMp4.SampleDelta, result.SampleDelta);
        Assert.Equal((ulong)SyntheticGameViewRecordingMp4.SampleCount, result.SampleCount);
        Assert.Equal(2, result.DurationSeconds);
        Assert.Equal(30, result.EffectiveFrameRate);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenCompactSizesAnd64BitOffsetsDescribeSamples_ReturnsVideoStructure ()
    {
        var bytes = SyntheticGameViewRecordingMp4.Create(
            useCompactSampleSizes: true,
            use64BitChunkOffsets: true);

        var result = await new GameViewRecordingMp4Validator().ValidateAsync(
            new MemoryStream(bytes, writable: false),
            SyntheticGameViewRecordingMp4.Width,
            SyntheticGameViewRecordingMp4.Height,
            expectedFrameRate: 30,
            expectedMaxDurationSeconds: 120,
            CancellationToken.None);

        Assert.Equal((ulong)SyntheticGameViewRecordingMp4.SampleCount, result.SampleCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenMediaDurationHasOneTickOfQuantization_ReturnsVideoStructure ()
    {
        var sampleDuration = (ulong)SyntheticGameViewRecordingMp4.SampleCount
            * SyntheticGameViewRecordingMp4.SampleDelta;
        var bytes = SyntheticGameViewRecordingMp4.Create(
            mediaDurationOverride: sampleDuration - 1);

        var result = await new GameViewRecordingMp4Validator().ValidateAsync(
            new MemoryStream(bytes, writable: false),
            SyntheticGameViewRecordingMp4.Width,
            SyntheticGameViewRecordingMp4.Height,
            expectedFrameRate: 30,
            expectedMaxDurationSeconds: 120,
            CancellationToken.None);

        Assert.Equal(sampleDuration, result.DurationInMediaTimeUnits);
        Assert.Equal(
            (sampleDuration - 1) / (double)SyntheticGameViewRecordingMp4.MediaTimescale,
            result.DurationSeconds);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("missingSps")]
    [InlineData("missingPps")]
    [InlineData("profileMismatch")]
    [InlineData("truncatedSps")]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenAvcSampleEntryDoesNotContainValidConfiguration_ThrowsInvalidDataException (
        string caseName)
    {
        var bytes = caseName switch
        {
            "missing" => SyntheticGameViewRecordingMp4.CreateWithoutAvcConfiguration(),
            "invalid" => SyntheticGameViewRecordingMp4.CreateWithInvalidAvcConfiguration(),
            "missingSps" => SyntheticGameViewRecordingMp4.CreateWithMissingSequenceParameterSets(),
            "missingPps" => SyntheticGameViewRecordingMp4.CreateWithMissingPictureParameterSets(),
            "profileMismatch" => SyntheticGameViewRecordingMp4.CreateWithMismatchedAvcProfile(),
            "truncatedSps" => SyntheticGameViewRecordingMp4.CreateWithTruncatedSequenceParameterSet(),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown AVC configuration case."),
        };

        await AssertInvalidAsync(bytes);
    }

    [Theory]
    [InlineData("emptySample")]
    [InlineData("emptyCompactSample")]
    [InlineData("emptyNalUnit")]
    [InlineData("invalidNalUnitType")]
    [InlineData("audOnly")]
    [InlineData("vclHeaderOnly")]
    [InlineData("unmappedSample")]
    [InlineData("chunkOutsideMediaData")]
    [InlineData("largeChunkOutsideMediaData")]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenVideoStorageDoesNotContainDeclaredAvcSamples_ThrowsInvalidDataException (
        string caseName)
    {
        var bytes = caseName switch
        {
            "emptySample" => SyntheticGameViewRecordingMp4.CreateWithEmptyDeclaredSamples(),
            "emptyCompactSample" => SyntheticGameViewRecordingMp4.CreateWithEmptyDeclaredSamples(
                useCompactSampleSizes: true),
            "emptyNalUnit" => SyntheticGameViewRecordingMp4.CreateWithEmptyNalUnit(),
            "invalidNalUnitType" => SyntheticGameViewRecordingMp4.CreateWithInvalidNalUnitType(),
            "audOnly" => SyntheticGameViewRecordingMp4.CreateWithAudOnlySamples(),
            "vclHeaderOnly" => SyntheticGameViewRecordingMp4.CreateWithVclHeaderOnlySamples(),
            "unmappedSample" => SyntheticGameViewRecordingMp4.CreateWithUnmappedSample(),
            "chunkOutsideMediaData" => SyntheticGameViewRecordingMp4.CreateWithChunkOutsideMediaData(),
            "largeChunkOutsideMediaData" => SyntheticGameViewRecordingMp4.CreateWithChunkOutsideMediaData(
                use64BitChunkOffsets: true),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown sample data case."),
        };

        await AssertInvalidAsync(bytes);
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("nestedBounds")]
    [InlineData("extendedOverflow")]
    [InlineData("extendedUnderflow")]
    [InlineData("missingMovie")]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenIsoBmffStructureIsIncompleteOrOutOfBounds_ThrowsInvalidDataException (
        string caseName)
    {
        var bytes = caseName switch
        {
            "truncated" => SyntheticGameViewRecordingMp4.CreateTruncatedMovie(),
            "nestedBounds" => SyntheticGameViewRecordingMp4.CreateNestedBoxOutsideParent(),
            "extendedOverflow" => SyntheticGameViewRecordingMp4.CreateExtendedSizeOverflow(),
            "extendedUnderflow" => SyntheticGameViewRecordingMp4.CreateUndersizedExtendedBox(),
            "missingMovie" => SyntheticGameViewRecordingMp4.CreateWithoutMovie(),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown corruption case."),
        };

        await AssertInvalidAsync(bytes);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("incompatible")]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenFileTypeDoesNotIdentifyOneMp4Profile_ThrowsInvalidDataException (
        string caseName)
    {
        var bytes = caseName switch
        {
            "missing" => SyntheticGameViewRecordingMp4.CreateWithoutFileType(),
            "duplicate" => SyntheticGameViewRecordingMp4.CreateWithDuplicateFileType(),
            "incompatible" => SyntheticGameViewRecordingMp4.CreateWithIncompatibleFileType(),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown file-type case."),
        };

        await AssertInvalidAsync(bytes);
    }

    [Theory]
    [InlineData("trackMatrix")]
    [InlineData("editList")]
    [InlineData("compositionOffsets")]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenPresentationMetadataChangesTheFixedRecordingProfile_ThrowsInvalidDataException (
        string caseName)
    {
        var bytes = caseName switch
        {
            "trackMatrix" => SyntheticGameViewRecordingMp4.CreateWithNonIdentityTrackMatrix(),
            "editList" => SyntheticGameViewRecordingMp4.CreateWithEditList(),
            "compositionOffsets" => SyntheticGameViewRecordingMp4.CreateWithCompositionTimeOffsets(),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown presentation case."),
        };

        await AssertInvalidAsync(bytes);
    }

    [Theory]
    [InlineData("trackHeader")]
    [InlineData("sampleEntry")]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenVideoDimensionsDoNotMatchRequest_ThrowsInvalidDataException (string caseName)
    {
        var bytes = caseName switch
        {
            "trackHeader" => SyntheticGameViewRecordingMp4.Create(
                trackWidth: SyntheticGameViewRecordingMp4.Width - 2),
            "sampleEntry" => SyntheticGameViewRecordingMp4.Create(
                sampleEntryWidth: SyntheticGameViewRecordingMp4.Width - 2),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown dimension case."),
        };

        await AssertInvalidAsync(bytes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenVideoCodecIsNotAvc_ThrowsInvalidDataException ()
    {
        await AssertInvalidAsync(SyntheticGameViewRecordingMp4.Create(codec: "hvc1"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenMovieContainsAudioTrack_ThrowsInvalidDataException ()
    {
        await AssertInvalidAsync(SyntheticGameViewRecordingMp4.Create(includeAudioTrack: true));
    }

    [Theory]
    [InlineData("variableInterval")]
    [InlineData("mediaDuration")]
    [InlineData("trackDuration")]
    [InlineData("movieDuration")]
    [InlineData("requestedFrameRate")]
    [InlineData("zeroSamples")]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenSampleTimingIsNotConstantAndConsistent_ThrowsInvalidDataException (
        string caseName)
    {
        IReadOnlyList<(uint SampleCount, uint SampleDelta)>? entries = caseName switch
        {
            "variableInterval" => [(30, 1000), (30, 1001)],
            "zeroSamples" => [(0, SyntheticGameViewRecordingMp4.SampleDelta)],
            _ => null,
        };
        var mediaDurationOverride = caseName == "mediaDuration"
            ? ((ulong)SyntheticGameViewRecordingMp4.SampleCount * SyntheticGameViewRecordingMp4.SampleDelta) + 2
            : (ulong?)null;
        var movieDurationOverride = caseName == "movieDuration"
            ? ((ulong)SyntheticGameViewRecordingMp4.SampleCount * SyntheticGameViewRecordingMp4.SampleDelta) + 1000
            : (ulong?)null;
        var trackDurationOverride = caseName == "trackDuration"
            ? ((ulong)SyntheticGameViewRecordingMp4.SampleCount * SyntheticGameViewRecordingMp4.SampleDelta) + 1000
            : (ulong?)null;
        var expectedFrameRate = caseName == "requestedFrameRate" ? 60 : 30;
        var bytes = SyntheticGameViewRecordingMp4.Create(
            timeToSampleEntries: entries,
            mediaDurationOverride: mediaDurationOverride,
            trackDurationOverride: trackDurationOverride,
            movieDurationOverride: movieDurationOverride);

        await AssertInvalidAsync(bytes, expectedFrameRate);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenMovieContainsMultipleVideoTracks_ThrowsInvalidDataException ()
    {
        await AssertInvalidAsync(SyntheticGameViewRecordingMp4.Create(videoTrackCount: 2));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenSampleCountEqualsRequestedLimit_ReturnsVideoStructure ()
    {
        var result = await new GameViewRecordingMp4Validator().ValidateAsync(
            new MemoryStream(SyntheticGameViewRecordingMp4.Create(), writable: false),
            SyntheticGameViewRecordingMp4.Width,
            SyntheticGameViewRecordingMp4.Height,
            expectedFrameRate: 30,
            expectedMaxDurationSeconds: 2,
            CancellationToken.None);

        Assert.Equal((ulong)SyntheticGameViewRecordingMp4.SampleCount, result.SampleCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenSampleCountExceedsRequestedLimitByOne_ThrowsInvalidDataException ()
    {
        var bytes = SyntheticGameViewRecordingMp4.Create(
            timeToSampleEntries:
            [
                (SyntheticGameViewRecordingMp4.SampleCount + 1, SyntheticGameViewRecordingMp4.SampleDelta),
            ]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new GameViewRecordingMp4Validator()
                .ValidateAsync(
                    new MemoryStream(bytes, writable: false),
                    SyntheticGameViewRecordingMp4.Width,
                    SyntheticGameViewRecordingMp4.Height,
                    expectedFrameRate: 30,
                    expectedMaxDurationSeconds: 2,
                    CancellationToken.None)
                .AsTask());

        Assert.Contains("exceeds the requested duration limit", exception.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenQuantizedPlaybackDurationExceedsRequestedLimitButSampleCountDoesNot_ReturnsVideoStructure ()
    {
        var sampleDuration = (ulong)SyntheticGameViewRecordingMp4.SampleCount
            * (SyntheticGameViewRecordingMp4.SampleDelta * 4);
        var quantizedDuration = sampleDuration + 1;
        var bytes = SyntheticGameViewRecordingMp4.Create(
            timeToSampleEntries:
            [
                (SyntheticGameViewRecordingMp4.SampleCount, SyntheticGameViewRecordingMp4.SampleDelta * 4),
            ],
            mediaDurationOverride: quantizedDuration,
            trackDurationOverride: quantizedDuration,
            movieDurationOverride: quantizedDuration,
            timescale: SyntheticGameViewRecordingMp4.MediaTimescale * 4);

        var result = await new GameViewRecordingMp4Validator().ValidateAsync(
            new MemoryStream(bytes, writable: false),
            SyntheticGameViewRecordingMp4.Width,
            SyntheticGameViewRecordingMp4.Height,
            expectedFrameRate: 30,
            expectedMaxDurationSeconds: 2,
            CancellationToken.None);

        Assert.True(result.DurationSeconds > 2);
        Assert.Equal((ulong)SyntheticGameViewRecordingMp4.SampleCount, result.SampleCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenSampleCountExceedsStructuralLimit_ThrowsInvalidDataException ()
    {
        var bytes = SyntheticGameViewRecordingMp4.Create(
            timeToSampleEntries:
            [
                (uint.MaxValue, SyntheticGameViewRecordingMp4.SampleDelta),
            ],
            mediaDurationOverride: SyntheticGameViewRecordingMp4.SampleDelta,
            trackDurationOverride: SyntheticGameViewRecordingMp4.SampleDelta,
            movieDurationOverride: SyntheticGameViewRecordingMp4.SampleDelta,
            includeMediaData: false);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new GameViewRecordingMp4Validator()
                .ValidateAsync(
                    new MemoryStream(bytes, writable: false),
                    SyntheticGameViewRecordingMp4.Width,
                    SyntheticGameViewRecordingMp4.Height,
                    expectedFrameRate: double.MaxValue,
                    expectedMaxDurationSeconds: int.MaxValue,
                    CancellationToken.None)
                .AsTask());

        Assert.Contains("stts sample count exceeds the supported structural limit", exception.Message);
    }

    private static async Task AssertInvalidAsync (
        byte[] bytes,
        double expectedFrameRate = 30,
        int expectedMaxDurationSeconds = 120)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new GameViewRecordingMp4Validator()
                .ValidateAsync(
                    new MemoryStream(bytes, writable: false),
                    SyntheticGameViewRecordingMp4.Width,
                    SyntheticGameViewRecordingMp4.Height,
                    expectedFrameRate,
                    expectedMaxDurationSeconds,
                    CancellationToken.None)
                .AsTask());
    }

    private sealed class ChunkedReadStream : Stream
    {
        private readonly int maximumReadSize;
        private readonly MemoryStream stream;

        public ChunkedReadStream (
            byte[] bytes,
            int maximumReadSize)
        {
            stream = new MemoryStream(bytes, writable: false);
            this.maximumReadSize = maximumReadSize;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => stream.Length;

        public override long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }

        public override void Flush ()
        {
        }

        public override int Read (
            byte[] buffer,
            int offset,
            int count)
        {
            return stream.Read(buffer, offset, Math.Min(count, maximumReadSize));
        }

        public override ValueTask<int> ReadAsync (
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return stream.ReadAsync(buffer[..Math.Min(buffer.Length, maximumReadSize)], cancellationToken);
        }

        public override long Seek (
            long offset,
            SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength (long value)
        {
            throw new NotSupportedException();
        }

        public override void Write (
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                stream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
