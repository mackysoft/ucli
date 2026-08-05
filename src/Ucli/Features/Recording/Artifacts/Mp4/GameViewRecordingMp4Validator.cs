using System.Buffers.Binary;
using System.Text;
using MackySoft.Ucli.Contracts.Presentation;

namespace MackySoft.Ucli.Features.Recording.Artifacts.Mp4;

/// <summary> Validates the ISO Base Media File Format subset emitted for one GameView recording. </summary>
internal sealed class GameViewRecordingMp4Validator
{
    // Bounds sample-table allocation and traversal independently of wall-clock duration,
    // which constant-frame-rate capture does not determine.
    private const uint MaximumStructuralSampleCount = 1_000_000;

    private const uint AudioHandlerType = 0x736F756E;
    private const uint Avc1SampleEntryType = 0x61766331;
    private const uint Avc3SampleEntryType = 0x61766333;
    private const uint AvcConfigurationBoxType = 0x61766343;
    private const uint ChunkLargeOffsetBoxType = 0x636F3634;
    private const uint ChunkOffsetBoxType = 0x7374636F;
    private const uint CompactSampleSizeBoxType = 0x73747A32;
    private const uint CompositionTimeToSampleBoxType = 0x63747473;
    private const uint EditBoxType = 0x65647473;
    private const uint EditListBoxType = 0x656C7374;
    private const uint FileTypeBoxType = 0x66747970;
    private const uint HandlerBoxType = 0x68646C72;
    private const uint MediaBoxType = 0x6D646961;
    private const uint MediaDataBoxType = 0x6D646174;
    private const uint MediaHeaderBoxType = 0x6D646864;
    private const uint MediaInformationBoxType = 0x6D696E66;
    private const uint MovieBoxType = 0x6D6F6F76;
    private const uint MovieHeaderBoxType = 0x6D766864;
    private const uint Mp41Brand = 0x6D703431;
    private const uint Mp42Brand = 0x6D703432;
    private const uint SampleDescriptionBoxType = 0x73747364;
    private const uint SampleSizeBoxType = 0x7374737A;
    private const uint SampleTableBoxType = 0x7374626C;
    private const uint SampleToChunkBoxType = 0x73747363;
    private const uint TimeToSampleBoxType = 0x73747473;
    private const uint TrackBoxType = 0x7472616B;
    private const uint TrackHeaderBoxType = 0x746B6864;
    private const uint VideoHandlerType = 0x76696465;
    private const double RelativeTimingTolerance = 0.00001;

    /// <summary> Validates one finalized MP4 without taking ownership of the borrowed stream. </summary>
    /// <param name="stream"> The readable, seekable MP4 stream positioned at the beginning of the file. </param>
    /// <param name="expectedWidth"> The requested video width. </param>
    /// <param name="expectedHeight"> The requested video height. </param>
    /// <param name="expectedFrameRate"> The requested constant frame rate. </param>
    /// <param name="cancellationToken"> The cancellation token observed while reading the stream. </param>
    /// <returns> The validated video format and timing. </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="stream" /> is not readable and seekable from the beginning of the file.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a requested dimension or frame rate cannot identify an MP4 video contract.
    /// </exception>
    /// <exception cref="InvalidDataException"> Thrown when the stream does not satisfy the recording MP4 contract. </exception>
    public async ValueTask<GameViewRecordingMp4ValidationResult> ValidateAsync (
        Stream stream,
        int expectedWidth,
        int expectedHeight,
        double expectedFrameRate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek || stream.Position != 0)
        {
            throw new ArgumentException(
                "MP4 stream must be readable, seekable, and positioned at the beginning of the file.",
                nameof(stream));
        }

        ValidateExpectedDimension(expectedWidth, nameof(expectedWidth));
        ValidateExpectedDimension(expectedHeight, nameof(expectedHeight));
        if (!double.IsFinite(expectedFrameRate) || expectedFrameRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedFrameRate),
                expectedFrameRate,
                "Expected frame rate must be positive and finite.");
        }

        var reader = new IsoBmffReader(stream, MaximumStructuralSampleCount);
        var fileTypeObserved = false;
        MovieInfo? movie = null;
        while (await reader.TryReadBoxHeaderAsync(parentEnd: null, cancellationToken).ConfigureAwait(false) is { } box)
        {
            if (box.Type == FileTypeBoxType)
            {
                if (fileTypeObserved)
                {
                    throw new InvalidDataException("MP4 must contain exactly one ftyp box.");
                }

                await ReadFileTypeAsync(reader, box, cancellationToken).ConfigureAwait(false);
                fileTypeObserved = true;
            }
            else if (box.Type == MovieBoxType)
            {
                if (movie is not null)
                {
                    throw new InvalidDataException("MP4 must contain exactly one moov box.");
                }

                movie = await ReadMovieAsync(reader, box, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await reader.SkipBoxAsync(box, cancellationToken).ConfigureAwait(false);
            }

            await reader.EnsureBoxConsumedAsync(box, cancellationToken).ConfigureAwait(false);
        }

        if (movie is null)
        {
            throw new InvalidDataException("MP4 does not contain a complete moov box.");
        }
        if (!fileTypeObserved)
        {
            throw new InvalidDataException("MP4 does not contain an ftyp box.");
        }

        var fileEnd = reader.Position;
        var result = await ValidateMovieAsync(
                reader,
                movie,
                fileEnd,
                expectedWidth,
                expectedHeight,
                expectedFrameRate,
                cancellationToken)
            .ConfigureAwait(false);
        reader.Seek(fileEnd);
        return result;
    }

    private static async ValueTask ReadFileTypeAsync (
        IsoBmffReader reader,
        BoxHeader box,
        CancellationToken cancellationToken)
    {
        var end = box.End ?? throw new InvalidDataException("ftyp must declare a finite box size.");
        if (end - reader.Position < 8 || ((end - reader.Position - 8) % 4) != 0)
        {
            throw new InvalidDataException("ftyp does not contain a complete brand list.");
        }

        var majorBrand = await reader.ReadUInt32Async(end, cancellationToken).ConfigureAwait(false);
        _ = await reader.ReadUInt32Async(end, cancellationToken).ConfigureAwait(false);
        var isMp4Compatible = majorBrand is Mp41Brand or Mp42Brand;
        while (reader.Position < end)
        {
            var compatibleBrand = await reader.ReadUInt32Async(end, cancellationToken).ConfigureAwait(false);
            isMp4Compatible |= compatibleBrand is Mp41Brand or Mp42Brand;
        }

        if (!isMp4Compatible)
        {
            throw new InvalidDataException("ftyp does not declare an MP4-compatible mp41 or mp42 brand.");
        }
    }

    private static async ValueTask<MovieInfo> ReadMovieAsync (
        IsoBmffReader reader,
        BoxHeader movieBox,
        CancellationToken cancellationToken)
    {
        MovieHeaderInfo? movieHeader = null;
        TrackInfo? videoTrack = null;
        while (await reader.TryReadBoxHeaderAsync(movieBox.End, cancellationToken).ConfigureAwait(false) is { } child)
        {
            switch (child.Type)
            {
                case MovieHeaderBoxType:
                    if (movieHeader is not null)
                    {
                        throw new InvalidDataException("moov contains more than one mvhd box.");
                    }

                    movieHeader = await ReadMovieHeaderAsync(reader, child, cancellationToken).ConfigureAwait(false);
                    break;
                case TrackBoxType:
                {
                    var track = await ReadTrackAsync(reader, child, cancellationToken).ConfigureAwait(false);
                    if (track.Media.HandlerType == AudioHandlerType)
                    {
                        throw new InvalidDataException("GameView recording MP4 must not contain an audio track.");
                    }

                    if (track.Media.HandlerType == VideoHandlerType)
                    {
                        if (videoTrack is not null)
                        {
                            throw new InvalidDataException(
                                "GameView recording MP4 must contain exactly one video track.");
                        }

                        videoTrack = track;
                    }

                    break;
                }
                default:
                    await reader.SkipBoxAsync(child, cancellationToken).ConfigureAwait(false);
                    break;
            }

            await reader.EnsureBoxConsumedAsync(child, cancellationToken).ConfigureAwait(false);
        }

        return new MovieInfo(
            movieHeader ?? throw new InvalidDataException("moov does not contain a complete mvhd box."),
            videoTrack);
    }

    private static async ValueTask<MovieHeaderInfo> ReadMovieHeaderAsync (
        IsoBmffReader reader,
        BoxHeader box,
        CancellationToken cancellationToken)
    {
        var version = await ReadFullBoxVersionAsync(reader, box, cancellationToken).ConfigureAwait(false);
        uint timescale;
        ulong duration;
        switch (version)
        {
            case 0:
                await reader.SkipAsync(8, box.End, cancellationToken).ConfigureAwait(false);
                timescale = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
                duration = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
                break;
            case 1:
                await reader.SkipAsync(16, box.End, cancellationToken).ConfigureAwait(false);
                timescale = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
                duration = await reader.ReadUInt64Async(box.End, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidDataException($"mvhd uses unsupported version {version}.");
        }

        await reader.SkipAsync(80, box.End, cancellationToken).ConfigureAwait(false);
        await reader.RequireBoxDataEndAsync(box, cancellationToken).ConfigureAwait(false);
        if (timescale == 0 || duration == 0)
        {
            throw new InvalidDataException("Finalized mvhd must declare a positive timescale and duration.");
        }

        return new MovieHeaderInfo(timescale, duration);
    }

    private static async ValueTask<TrackInfo> ReadTrackAsync (
        IsoBmffReader reader,
        BoxHeader trackBox,
        CancellationToken cancellationToken)
    {
        TrackHeaderInfo? trackHeader = null;
        MediaInfo? media = null;
        while (await reader.TryReadBoxHeaderAsync(trackBox.End, cancellationToken).ConfigureAwait(false) is { } child)
        {
            switch (child.Type)
            {
                case TrackHeaderBoxType:
                    if (trackHeader is not null)
                    {
                        throw new InvalidDataException("trak contains more than one tkhd box.");
                    }

                    trackHeader = await ReadTrackHeaderAsync(reader, child, cancellationToken).ConfigureAwait(false);
                    break;
                case MediaBoxType:
                    if (media is not null)
                    {
                        throw new InvalidDataException("trak contains more than one mdia box.");
                    }

                    media = await ReadMediaAsync(reader, child, cancellationToken).ConfigureAwait(false);
                    break;
                case EditBoxType:
                case EditListBoxType:
                    throw new InvalidDataException(
                        "The fixed GameView recording profile does not permit track edit lists.");
                default:
                    await reader.SkipBoxAsync(child, cancellationToken).ConfigureAwait(false);
                    break;
            }

            await reader.EnsureBoxConsumedAsync(child, cancellationToken).ConfigureAwait(false);
        }

        return new TrackInfo(
            trackHeader ?? throw new InvalidDataException("trak does not contain a complete tkhd box."),
            media ?? throw new InvalidDataException("trak does not contain a complete mdia box."));
    }

    private static async ValueTask<TrackHeaderInfo> ReadTrackHeaderAsync (
        IsoBmffReader reader,
        BoxHeader box,
        CancellationToken cancellationToken)
    {
        var version = await ReadFullBoxVersionAsync(reader, box, cancellationToken).ConfigureAwait(false);
        ulong duration;
        switch (version)
        {
            case 0:
                await reader.SkipAsync(16, box.End, cancellationToken).ConfigureAwait(false);
                duration = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
                break;
            case 1:
                await reader.SkipAsync(24, box.End, cancellationToken).ConfigureAwait(false);
                duration = await reader.ReadUInt64Async(box.End, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidDataException($"tkhd uses unsupported version {version}.");
        }

        await reader.SkipAsync(16, box.End, cancellationToken).ConfigureAwait(false);
        await RequireIdentityTrackMatrixAsync(reader, box, cancellationToken).ConfigureAwait(false);
        var width = ReadFixedPointDimension(
            await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false),
            "tkhd width");
        var height = ReadFixedPointDimension(
            await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false),
            "tkhd height");
        await reader.RequireBoxDataEndAsync(box, cancellationToken).ConfigureAwait(false);
        return new TrackHeaderInfo(width, height, duration);
    }

    private static async ValueTask RequireIdentityTrackMatrixAsync (
        IsoBmffReader reader,
        BoxHeader box,
        CancellationToken cancellationToken)
    {
        uint[] identity =
        [
            0x00010000,
            0,
            0,
            0,
            0x00010000,
            0,
            0,
            0,
            0x40000000,
        ];
        for (var index = 0; index < identity.Length; index++)
        {
            var value = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
            if (value != identity[index])
            {
                throw new InvalidDataException(
                    "GameView recording video tracks must use the identity presentation matrix.");
            }
        }
    }

    private static async ValueTask<MediaInfo> ReadMediaAsync (
        IsoBmffReader reader,
        BoxHeader mediaBox,
        CancellationToken cancellationToken)
    {
        MediaHeaderInfo? mediaHeader = null;
        uint? handlerType = null;
        SampleTableInfo? sampleTable = null;
        var foundMediaInformation = false;
        while (await reader.TryReadBoxHeaderAsync(mediaBox.End, cancellationToken).ConfigureAwait(false) is { } child)
        {
            switch (child.Type)
            {
                case MediaHeaderBoxType:
                    if (mediaHeader is not null)
                    {
                        throw new InvalidDataException("mdia contains more than one mdhd box.");
                    }

                    mediaHeader = await ReadMediaHeaderAsync(reader, child, cancellationToken).ConfigureAwait(false);
                    break;
                case HandlerBoxType:
                    if (handlerType is not null)
                    {
                        throw new InvalidDataException("mdia contains more than one hdlr box.");
                    }

                    handlerType = await ReadHandlerTypeAsync(reader, child, cancellationToken).ConfigureAwait(false);
                    break;
                case MediaInformationBoxType:
                    if (foundMediaInformation)
                    {
                        throw new InvalidDataException("mdia contains more than one minf box.");
                    }

                    foundMediaInformation = true;
                    sampleTable = await ReadMediaInformationAsync(reader, child, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    await reader.SkipBoxAsync(child, cancellationToken).ConfigureAwait(false);
                    break;
            }

            await reader.EnsureBoxConsumedAsync(child, cancellationToken).ConfigureAwait(false);
        }

        return new MediaInfo(
            mediaHeader ?? throw new InvalidDataException("mdia does not contain a complete mdhd box."),
            handlerType ?? throw new InvalidDataException("mdia does not contain a complete hdlr box."),
            sampleTable);
    }

    private static async ValueTask<MediaHeaderInfo> ReadMediaHeaderAsync (
        IsoBmffReader reader,
        BoxHeader box,
        CancellationToken cancellationToken)
    {
        var version = await ReadFullBoxVersionAsync(reader, box, cancellationToken).ConfigureAwait(false);
        uint timescale;
        ulong duration;
        switch (version)
        {
            case 0:
                await reader.SkipAsync(8, box.End, cancellationToken).ConfigureAwait(false);
                timescale = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
                duration = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
                break;
            case 1:
                await reader.SkipAsync(16, box.End, cancellationToken).ConfigureAwait(false);
                timescale = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
                duration = await reader.ReadUInt64Async(box.End, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidDataException($"mdhd uses unsupported version {version}.");
        }

        await reader.SkipAsync(4, box.End, cancellationToken).ConfigureAwait(false);
        await reader.RequireBoxDataEndAsync(box, cancellationToken).ConfigureAwait(false);
        if (timescale == 0)
        {
            throw new InvalidDataException("mdhd timescale must be positive.");
        }

        return new MediaHeaderInfo(timescale, duration);
    }

    private static async ValueTask<uint> ReadHandlerTypeAsync (
        IsoBmffReader reader,
        BoxHeader box,
        CancellationToken cancellationToken)
    {
        var version = await ReadFullBoxVersionAsync(reader, box, cancellationToken).ConfigureAwait(false);
        if (version != 0)
        {
            throw new InvalidDataException($"hdlr uses unsupported version {version}.");
        }

        await reader.SkipAsync(4, box.End, cancellationToken).ConfigureAwait(false);
        var handlerType = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
        await reader.SkipBoxAsync(box, cancellationToken).ConfigureAwait(false);
        return handlerType;
    }

    private static async ValueTask<SampleTableInfo?> ReadMediaInformationAsync (
        IsoBmffReader reader,
        BoxHeader mediaInformationBox,
        CancellationToken cancellationToken)
    {
        SampleTableInfo? sampleTable = null;
        while (await reader.TryReadBoxHeaderAsync(mediaInformationBox.End, cancellationToken).ConfigureAwait(false) is { } child)
        {
            if (child.Type == SampleTableBoxType)
            {
                if (sampleTable is not null)
                {
                    throw new InvalidDataException("minf contains more than one stbl box.");
                }

                sampleTable = await ReadSampleTableAsync(reader, child, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await reader.SkipBoxAsync(child, cancellationToken).ConfigureAwait(false);
            }

            await reader.EnsureBoxConsumedAsync(child, cancellationToken).ConfigureAwait(false);
        }

        return sampleTable;
    }

    private static async ValueTask<SampleTableInfo> ReadSampleTableAsync (
        IsoBmffReader reader,
        BoxHeader sampleTableBox,
        CancellationToken cancellationToken)
    {
        SampleDescriptionInfo? sampleDescription = null;
        TimeToSampleInfo? timeToSample = null;
        SampleSizeInfo? sampleSize = null;
        SampleToChunkInfo? sampleToChunk = null;
        ChunkOffsetInfo? chunkOffset = null;
        while (await reader.TryReadBoxHeaderAsync(sampleTableBox.End, cancellationToken).ConfigureAwait(false) is { } child)
        {
            switch (child.Type)
            {
                case SampleDescriptionBoxType:
                    if (sampleDescription is not null)
                    {
                        throw new InvalidDataException("stbl contains more than one stsd box.");
                    }

                    sampleDescription = await ReadSampleDescriptionAsync(reader, child, cancellationToken).ConfigureAwait(false);
                    break;
                case TimeToSampleBoxType:
                    if (timeToSample is not null)
                    {
                        throw new InvalidDataException("stbl contains more than one stts box.");
                    }

                    timeToSample = await ReadTimeToSampleAsync(reader, child, cancellationToken).ConfigureAwait(false);
                    break;
                case CompositionTimeToSampleBoxType:
                    throw new InvalidDataException(
                        "The fixed GameView recording profile does not permit composition-time offsets.");
                case SampleSizeBoxType:
                    if (sampleSize is not null)
                    {
                        throw new InvalidDataException("stbl contains more than one sample-size box.");
                    }

                    sampleSize = await ReadSampleSizeAsync(reader, child, cancellationToken).ConfigureAwait(false);
                    break;
                case CompactSampleSizeBoxType:
                    if (sampleSize is not null)
                    {
                        throw new InvalidDataException("stbl contains more than one sample-size box.");
                    }

                    sampleSize = await ReadCompactSampleSizeAsync(reader, child, cancellationToken).ConfigureAwait(false);
                    break;
                case SampleToChunkBoxType:
                    if (sampleToChunk is not null)
                    {
                        throw new InvalidDataException("stbl contains more than one stsc box.");
                    }

                    sampleToChunk = await ReadSampleToChunkAsync(reader, child, cancellationToken).ConfigureAwait(false);
                    break;
                case ChunkOffsetBoxType:
                    if (chunkOffset is not null)
                    {
                        throw new InvalidDataException("stbl contains more than one chunk-offset box.");
                    }

                    chunkOffset = await ReadChunkOffsetAsync(
                            reader,
                            child,
                            uses64BitOffsets: false,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case ChunkLargeOffsetBoxType:
                    if (chunkOffset is not null)
                    {
                        throw new InvalidDataException("stbl contains more than one chunk-offset box.");
                    }

                    chunkOffset = await ReadChunkOffsetAsync(
                            reader,
                            child,
                            uses64BitOffsets: true,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                default:
                    await reader.SkipBoxAsync(child, cancellationToken).ConfigureAwait(false);
                    break;
            }

            await reader.EnsureBoxConsumedAsync(child, cancellationToken).ConfigureAwait(false);
        }

        return new SampleTableInfo(
            sampleDescription,
            timeToSample,
            sampleSize,
            sampleToChunk,
            chunkOffset);
    }

    private static async ValueTask<SampleDescriptionInfo> ReadSampleDescriptionAsync (
        IsoBmffReader reader,
        BoxHeader box,
        CancellationToken cancellationToken)
    {
        var version = await ReadFullBoxVersionAsync(reader, box, cancellationToken).ConfigureAwait(false);
        if (version != 0)
        {
            throw new InvalidDataException($"stsd uses unsupported version {version}.");
        }

        var entryCount = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
        SampleEntryInfo? firstEntry = null;
        for (uint entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            var entry = await reader.TryReadBoxHeaderAsync(box.End, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("stsd ended before all declared sample entries were read.");
            SampleEntryInfo entryInfo;
            if (entry.Type is Avc1SampleEntryType or Avc3SampleEntryType)
            {
                entryInfo = await ReadAvcSampleEntryAsync(reader, entry, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                entryInfo = new SampleEntryInfo(entry.Type, null, null, NalLengthSize: null);
                await reader.SkipBoxAsync(entry, cancellationToken).ConfigureAwait(false);
            }

            await reader.EnsureBoxConsumedAsync(entry, cancellationToken).ConfigureAwait(false);
            firstEntry ??= entryInfo;
        }

        await reader.RequireBoxDataEndAsync(box, cancellationToken).ConfigureAwait(false);
        return new SampleDescriptionInfo(entryCount, firstEntry);
    }

    private static async ValueTask<SampleEntryInfo> ReadAvcSampleEntryAsync (
        IsoBmffReader reader,
        BoxHeader entry,
        CancellationToken cancellationToken)
    {
        await reader.SkipAsync(24, entry.End, cancellationToken).ConfigureAwait(false);
        var width = await reader.ReadUInt16Async(entry.End, cancellationToken).ConfigureAwait(false);
        var height = await reader.ReadUInt16Async(entry.End, cancellationToken).ConfigureAwait(false);
        await reader.SkipAsync(50, entry.End, cancellationToken).ConfigureAwait(false);

        int? nalLengthSize = null;
        while (await reader.TryReadBoxHeaderAsync(entry.End, cancellationToken).ConfigureAwait(false) is { } child)
        {
            if (child.Type == AvcConfigurationBoxType)
            {
                if (nalLengthSize is not null)
                {
                    throw new InvalidDataException("AVC sample entry contains more than one avcC box.");
                }

                nalLengthSize = await ReadAvcConfigurationAsync(
                        reader,
                        child,
                        width,
                        height,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await reader.SkipBoxAsync(child, cancellationToken).ConfigureAwait(false);
            }

            await reader.EnsureBoxConsumedAsync(child, cancellationToken).ConfigureAwait(false);
        }

        return new SampleEntryInfo(entry.Type, width, height, nalLengthSize);
    }

    private static async ValueTask<int> ReadAvcConfigurationAsync (
        IsoBmffReader reader,
        BoxHeader box,
        int expectedWidth,
        int expectedHeight,
        CancellationToken cancellationToken)
    {
        var configurationVersion = await reader.ReadByteAsync(box.End, cancellationToken).ConfigureAwait(false);
        if (configurationVersion != 1)
        {
            throw new InvalidDataException(
                $"avcC uses unsupported configurationVersion {configurationVersion}.");
        }

        var profile = await reader.ReadByteAsync(box.End, cancellationToken).ConfigureAwait(false);
        var profileCompatibility = await reader.ReadByteAsync(box.End, cancellationToken).ConfigureAwait(false);
        var level = await reader.ReadByteAsync(box.End, cancellationToken).ConfigureAwait(false);
        var lengthSizeField = await reader.ReadByteAsync(box.End, cancellationToken).ConfigureAwait(false);
        if ((lengthSizeField & 0xFC) != 0xFC || (lengthSizeField & 0x03) == 2)
        {
            throw new InvalidDataException("avcC contains an invalid NAL-unit length-size field.");
        }

        var sequenceParameterSetField = await reader.ReadByteAsync(box.End, cancellationToken).ConfigureAwait(false);
        if ((sequenceParameterSetField & 0xE0) != 0xE0)
        {
            throw new InvalidDataException("avcC contains invalid sequence-parameter-set reserved bits.");
        }

        var sequenceParameterSetCount = sequenceParameterSetField & 0x1F;
        if (sequenceParameterSetCount == 0)
        {
            throw new InvalidDataException("avcC must contain at least one sequence parameter set.");
        }

        var sequenceParameterSets = new Dictionary<uint, SequenceParameterSetInfo>();
        for (var index = 0; index < sequenceParameterSetCount; index++)
        {
            var parameterSet = await ReadAvcParameterSetAsync(
                    reader,
                    box,
                    expectedNalUnitType: 7,
                    cancellationToken)
                .ConfigureAwait(false);
            var sequenceParameterSet = ParseSequenceParameterSet(parameterSet);
            if (sequenceParameterSet.Profile != profile
                || sequenceParameterSet.ProfileCompatibility != profileCompatibility
                || sequenceParameterSet.Level != level)
            {
                throw new InvalidDataException(
                    "avcC profile, compatibility, and level fields must match every sequence parameter set.");
            }
            if (sequenceParameterSet.Width != expectedWidth
                || sequenceParameterSet.Height != expectedHeight)
            {
                throw new InvalidDataException(
                    $"AVC sequence parameter set dimensions do not match the visual sample entry. "
                    + $"Expected={expectedWidth}x{expectedHeight}, "
                    + $"Actual={sequenceParameterSet.Width}x{sequenceParameterSet.Height}.");
            }
            if (!sequenceParameterSets.TryAdd(sequenceParameterSet.Id, sequenceParameterSet))
            {
                throw new InvalidDataException("avcC contains duplicate sequence parameter set identifiers.");
            }
        }

        var pictureParameterSetCount = await reader.ReadByteAsync(box.End, cancellationToken).ConfigureAwait(false);
        if (pictureParameterSetCount == 0)
        {
            throw new InvalidDataException("avcC must contain at least one picture parameter set.");
        }

        var pictureParameterSetIds = new HashSet<uint>();
        for (var index = 0; index < pictureParameterSetCount; index++)
        {
            var parameterSet = await ReadAvcParameterSetAsync(
                    reader,
                    box,
                    expectedNalUnitType: 8,
                    cancellationToken)
                .ConfigureAwait(false);
            var pictureParameterSet = ParsePictureParameterSet(parameterSet, sequenceParameterSets);
            if (!pictureParameterSetIds.Add(pictureParameterSet.Id))
            {
                throw new InvalidDataException("avcC contains duplicate picture parameter set identifiers.");
            }
        }

        // Later AVC profiles may append profile-specific fields after the mandatory decoder configuration.
        await reader.SkipBoxAsync(box, cancellationToken).ConfigureAwait(false);
        return (lengthSizeField & 0x03) + 1;
    }

    private static async ValueTask<byte[]> ReadAvcParameterSetAsync (
        IsoBmffReader reader,
        BoxHeader box,
        byte expectedNalUnitType,
        CancellationToken cancellationToken)
    {
        var parameterSetLength = await reader.ReadUInt16Async(box.End, cancellationToken).ConfigureAwait(false);
        if (parameterSetLength == 0)
        {
            throw new InvalidDataException("avcC parameter sets must not be empty.");
        }

        var parameterSet = new byte[parameterSetLength];
        for (var index = 0; index < parameterSet.Length; index++)
        {
            parameterSet[index] = await reader.ReadByteAsync(box.End, cancellationToken).ConfigureAwait(false);
        }

        var nalHeader = parameterSet[0];
        if ((nalHeader & 0x80) != 0 || (nalHeader & 0x1F) != expectedNalUnitType)
        {
            throw new InvalidDataException(
                $"avcC parameter set does not contain NAL-unit type {expectedNalUnitType}.");
        }

        return parameterSet;
    }

    private static SequenceParameterSetInfo ParseSequenceParameterSet (byte[] parameterSet)
    {
        if (parameterSet.Length < 5 || (parameterSet[0] & 0x60) == 0)
        {
            throw new InvalidDataException("AVC sequence parameter set is truncated or non-reference data.");
        }

        var bits = new AvcBitReader(RemoveEmulationPreventionBytes(parameterSet.AsSpan(1)));
        var profile = checked((byte)bits.ReadBits(8));
        var profileCompatibility = checked((byte)bits.ReadBits(8));
        if ((profileCompatibility & 0x03) != 0)
        {
            throw new InvalidDataException("AVC sequence parameter set contains non-zero reserved constraint bits.");
        }

        var level = checked((byte)bits.ReadBits(8));
        var sequenceParameterSetId = bits.ReadUnsignedExpGolomb(maximumValue: 31);
        uint chromaFormatIdc = 1;
        var separateColourPlane = false;
        if (IsExtendedAvcProfile(profile))
        {
            chromaFormatIdc = bits.ReadUnsignedExpGolomb(maximumValue: 3);
            if (chromaFormatIdc == 3)
            {
                separateColourPlane = bits.ReadFlag();
            }

            _ = bits.ReadUnsignedExpGolomb(maximumValue: 6);
            _ = bits.ReadUnsignedExpGolomb(maximumValue: 6);
            _ = bits.ReadFlag();
            if (bits.ReadFlag())
            {
                var scalingListCount = chromaFormatIdc == 3 ? 12 : 8;
                for (var index = 0; index < scalingListCount; index++)
                {
                    if (bits.ReadFlag())
                    {
                        ReadAvcScalingList(bits, index < 6 ? 16 : 64);
                    }
                }
            }
        }

        _ = bits.ReadUnsignedExpGolomb(maximumValue: 12);
        var pictureOrderCountType = bits.ReadUnsignedExpGolomb(maximumValue: 2);
        switch (pictureOrderCountType)
        {
            case 0:
                _ = bits.ReadUnsignedExpGolomb(maximumValue: 12);
                break;
            case 1:
                _ = bits.ReadFlag();
                _ = bits.ReadSignedExpGolomb();
                _ = bits.ReadSignedExpGolomb();
                var referenceFrameCycleCount = bits.ReadUnsignedExpGolomb(maximumValue: 255);
                for (var index = 0; index < referenceFrameCycleCount; index++)
                {
                    _ = bits.ReadSignedExpGolomb();
                }

                break;
        }

        _ = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
        _ = bits.ReadFlag();
        var pictureWidthInMacroblocksMinusOne = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
        var pictureHeightInMapUnitsMinusOne = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
        var frameMacroblocksOnly = bits.ReadFlag();
        if (!frameMacroblocksOnly)
        {
            _ = bits.ReadFlag();
        }

        _ = bits.ReadFlag();
        uint cropLeft = 0;
        uint cropRight = 0;
        uint cropTop = 0;
        uint cropBottom = 0;
        if (bits.ReadFlag())
        {
            cropLeft = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
            cropRight = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
            cropTop = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
            cropBottom = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
        }

        if (bits.ReadFlag())
        {
            ReadAvcVideoUsabilityInformation(bits, separateColourPlane ? 0 : chromaFormatIdc);
        }

        bits.ReadRbspTrailingBits();

        var frameHeightMultiplier = frameMacroblocksOnly ? 1u : 2u;
        var rawWidth = checked((pictureWidthInMacroblocksMinusOne + 1) * 16);
        var rawHeight = checked(frameHeightMultiplier * (pictureHeightInMapUnitsMinusOne + 1) * 16);
        var chromaArrayType = separateColourPlane ? 0u : chromaFormatIdc;
        var cropUnitX = chromaArrayType switch
        {
            0 => 1u,
            1 or 2 => 2u,
            3 => 1u,
            _ => throw new InvalidDataException("AVC sequence parameter set uses an unsupported chroma format."),
        };
        var chromaCropHeight = chromaArrayType switch
        {
            0 or 2 or 3 => 1u,
            1 => 2u,
            _ => throw new InvalidDataException("AVC sequence parameter set uses an unsupported chroma format."),
        };
        var cropUnitY = checked(chromaCropHeight * frameHeightMultiplier);
        var croppedWidth = checked((cropLeft + cropRight) * cropUnitX);
        var croppedHeight = checked((cropTop + cropBottom) * cropUnitY);
        if (croppedWidth >= rawWidth || croppedHeight >= rawHeight)
        {
            throw new InvalidDataException("AVC sequence parameter set cropping removes the complete coded frame.");
        }

        return new SequenceParameterSetInfo(
            sequenceParameterSetId,
            profile,
            profileCompatibility,
            level,
            chromaFormatIdc,
            checked((int)(rawWidth - croppedWidth)),
            checked((int)(rawHeight - croppedHeight)));
    }

    private static PictureParameterSetInfo ParsePictureParameterSet (
        byte[] parameterSet,
        IReadOnlyDictionary<uint, SequenceParameterSetInfo> sequenceParameterSets)
    {
        if (parameterSet.Length < 2 || (parameterSet[0] & 0x60) == 0)
        {
            throw new InvalidDataException("AVC picture parameter set is truncated or non-reference data.");
        }

        var bits = new AvcBitReader(RemoveEmulationPreventionBytes(parameterSet.AsSpan(1)));
        var pictureParameterSetId = bits.ReadUnsignedExpGolomb(maximumValue: 255);
        var sequenceParameterSetId = bits.ReadUnsignedExpGolomb(maximumValue: 31);
        if (!sequenceParameterSets.TryGetValue(sequenceParameterSetId, out var sequenceParameterSet))
        {
            throw new InvalidDataException("AVC picture parameter set refers to an undeclared sequence parameter set.");
        }

        _ = bits.ReadFlag();
        _ = bits.ReadFlag();
        var sliceGroupCountMinusOne = bits.ReadUnsignedExpGolomb(maximumValue: 7);
        if (sliceGroupCountMinusOne != 0)
        {
            var sliceGroupMapType = bits.ReadUnsignedExpGolomb(maximumValue: 6);
            switch (sliceGroupMapType)
            {
                case 0:
                    for (var index = 0; index <= sliceGroupCountMinusOne; index++)
                    {
                        _ = bits.ReadUnsignedExpGolomb(maximumValue: 1_048_575);
                    }

                    break;
                case 2:
                    for (var index = 0; index < sliceGroupCountMinusOne; index++)
                    {
                        _ = bits.ReadUnsignedExpGolomb(maximumValue: 1_048_575);
                        _ = bits.ReadUnsignedExpGolomb(maximumValue: 1_048_575);
                    }

                    break;
                case 3 or 4 or 5:
                    _ = bits.ReadFlag();
                    _ = bits.ReadUnsignedExpGolomb(maximumValue: 1_048_575);
                    break;
                case 6:
                    var pictureSizeInMapUnitsMinusOne = bits.ReadUnsignedExpGolomb(maximumValue: 1_048_575);
                    var sliceGroupIdBitCount = GetCeilingLog2(sliceGroupCountMinusOne + 1);
                    for (var index = 0; index <= pictureSizeInMapUnitsMinusOne; index++)
                    {
                        _ = bits.ReadBits(sliceGroupIdBitCount);
                    }

                    break;
            }
        }

        _ = bits.ReadUnsignedExpGolomb(maximumValue: 31);
        _ = bits.ReadUnsignedExpGolomb(maximumValue: 31);
        _ = bits.ReadFlag();
        _ = bits.ReadBits(2);
        _ = bits.ReadSignedExpGolomb();
        _ = bits.ReadSignedExpGolomb();
        _ = bits.ReadSignedExpGolomb();
        _ = bits.ReadFlag();
        _ = bits.ReadFlag();
        _ = bits.ReadFlag();
        if (bits.MoreRbspData())
        {
            var transform8By8Mode = bits.ReadFlag();
            if (bits.ReadFlag())
            {
                var scalingListCount = 6 + ((sequenceParameterSet.ChromaFormatIdc != 3 ? 2 : 6)
                    * (transform8By8Mode ? 1 : 0));
                for (var index = 0; index < scalingListCount; index++)
                {
                    if (bits.ReadFlag())
                    {
                        ReadAvcScalingList(bits, index < 6 ? 16 : 64);
                    }
                }
            }

            _ = bits.ReadSignedExpGolomb();
        }

        bits.ReadRbspTrailingBits();
        return new PictureParameterSetInfo(pictureParameterSetId, sequenceParameterSetId);
    }

    private static void ReadAvcVideoUsabilityInformation (
        AvcBitReader bits,
        uint chromaArrayType)
    {
        if (bits.ReadFlag())
        {
            var aspectRatioIdc = bits.ReadBits(8);
            if (aspectRatioIdc == 255)
            {
                _ = bits.ReadBits(16);
                _ = bits.ReadBits(16);
            }
        }
        if (bits.ReadFlag())
        {
            _ = bits.ReadFlag();
        }
        if (bits.ReadFlag())
        {
            _ = bits.ReadBits(3);
            _ = bits.ReadFlag();
            if (bits.ReadFlag())
            {
                _ = bits.ReadBits(8);
                _ = bits.ReadBits(8);
                _ = bits.ReadBits(8);
            }
        }
        if (chromaArrayType != 0 && bits.ReadFlag())
        {
            _ = bits.ReadUnsignedExpGolomb(maximumValue: 5);
            _ = bits.ReadUnsignedExpGolomb(maximumValue: 5);
        }
        if (bits.ReadFlag())
        {
            _ = bits.ReadBits(32);
            _ = bits.ReadBits(32);
            _ = bits.ReadFlag();
        }

        var nalHrdPresent = bits.ReadFlag();
        if (nalHrdPresent)
        {
            ReadAvcHypotheticalReferenceDecoderParameters(bits);
        }
        var vclHrdPresent = bits.ReadFlag();
        if (vclHrdPresent)
        {
            ReadAvcHypotheticalReferenceDecoderParameters(bits);
        }
        if (nalHrdPresent || vclHrdPresent)
        {
            _ = bits.ReadFlag();
        }

        _ = bits.ReadFlag();
        if (bits.ReadFlag())
        {
            _ = bits.ReadFlag();
            _ = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
            _ = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
            _ = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
            _ = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
            _ = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
            _ = bits.ReadUnsignedExpGolomb(maximumValue: 65_535);
        }
    }

    private static void ReadAvcHypotheticalReferenceDecoderParameters (AvcBitReader bits)
    {
        var codedPictureBufferCountMinusOne = bits.ReadUnsignedExpGolomb(maximumValue: 31);
        _ = bits.ReadBits(4);
        _ = bits.ReadBits(4);
        for (var index = 0; index <= codedPictureBufferCountMinusOne; index++)
        {
            _ = bits.ReadUnsignedExpGolomb(maximumValue: uint.MaxValue - 1);
            _ = bits.ReadUnsignedExpGolomb(maximumValue: uint.MaxValue - 1);
            _ = bits.ReadFlag();
        }

        _ = bits.ReadBits(5);
        _ = bits.ReadBits(5);
        _ = bits.ReadBits(5);
        _ = bits.ReadBits(5);
    }

    private static void ReadAvcScalingList (
        AvcBitReader bits,
        int size)
    {
        var lastScale = 8;
        var nextScale = 8;
        for (var index = 0; index < size; index++)
        {
            if (nextScale != 0)
            {
                var deltaScale = bits.ReadSignedExpGolomb();
                nextScale = (lastScale + deltaScale + 256) % 256;
            }

            lastScale = nextScale == 0 ? lastScale : nextScale;
        }
    }

    private static bool IsExtendedAvcProfile (byte profile)
    {
        return profile is 44 or 83 or 86 or 100 or 110 or 118 or 122 or 128 or 134 or 135 or 138 or 139 or 244;
    }

    private static int GetCeilingLog2 (uint value)
    {
        var bits = 0;
        var maximumRepresentableValue = 1u;
        while (maximumRepresentableValue < value)
        {
            bits++;
            maximumRepresentableValue <<= 1;
        }

        return Math.Max(bits, 1);
    }

    private static byte[] RemoveEmulationPreventionBytes (ReadOnlySpan<byte> encoded)
    {
        var decoded = new List<byte>(encoded.Length);
        var consecutiveZeros = 0;
        for (var index = 0; index < encoded.Length; index++)
        {
            var value = encoded[index];
            if (consecutiveZeros >= 2
                && value == 3
                && index + 1 < encoded.Length
                && encoded[index + 1] <= 3)
            {
                consecutiveZeros = 0;
                continue;
            }

            decoded.Add(value);
            consecutiveZeros = value == 0 ? consecutiveZeros + 1 : 0;
        }

        return decoded.ToArray();
    }

    private static async ValueTask<TimeToSampleInfo> ReadTimeToSampleAsync (
        IsoBmffReader reader,
        BoxHeader box,
        CancellationToken cancellationToken)
    {
        var version = await ReadFullBoxVersionAsync(reader, box, cancellationToken).ConfigureAwait(false);
        if (version != 0)
        {
            throw new InvalidDataException($"stts uses unsupported version {version}.");
        }

        var entryCount = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
        RequireBoundedTableData(
            reader,
            box,
            entryCount,
            (ulong)entryCount * 8,
            "stts");
        uint? constantDelta = null;
        ulong sampleCount = 0;
        for (uint entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            var entrySampleCount = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
            var entrySampleDelta = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
            if (entrySampleDelta == 0)
            {
                throw new InvalidDataException("stts sample_delta must be positive.");
            }

            if (constantDelta is { } establishedDelta && establishedDelta != entrySampleDelta)
            {
                throw new InvalidDataException("GameView recording stts entries must use one constant sample_delta.");
            }

            constantDelta ??= entrySampleDelta;
            if (entrySampleCount > (ulong)reader.MaximumSampleCount - sampleCount)
            {
                throw new InvalidDataException(
                    $"stts sample count exceeds the supported structural limit of {reader.MaximumSampleCount}.");
            }

            try
            {
                sampleCount = checked(sampleCount + entrySampleCount);
            }
            catch (OverflowException exception)
            {
                throw new InvalidDataException("stts sample count exceeds the supported range.", exception);
            }
        }

        await reader.RequireBoxDataEndAsync(box, cancellationToken).ConfigureAwait(false);
        return new TimeToSampleInfo(sampleCount, constantDelta);
    }

    private static async ValueTask<SampleSizeInfo> ReadSampleSizeAsync (
        IsoBmffReader reader,
        BoxHeader box,
        CancellationToken cancellationToken)
    {
        var version = await ReadFullBoxVersionAsync(reader, box, cancellationToken).ConfigureAwait(false);
        if (version != 0)
        {
            throw new InvalidDataException($"stsz uses unsupported version {version}.");
        }

        var uniformSampleSize = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
        var sampleCount = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
        if (uniformSampleSize != 0)
        {
            RequireBoundedTableData(reader, box, sampleCount, requiredByteCount: 0, "stsz");
            await reader.RequireBoxDataEndAsync(box, cancellationToken).ConfigureAwait(false);
            return new SampleSizeInfo(sampleCount, uniformSampleSize, Sizes: null);
        }

        RequireBoundedTableData(
            reader,
            box,
            sampleCount,
            (ulong)sampleCount * 4,
            "stsz");
        var sizes = new List<uint>(checked((int)sampleCount));
        for (uint sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            sizes.Add(await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false));
        }

        await reader.RequireBoxDataEndAsync(box, cancellationToken).ConfigureAwait(false);
        return new SampleSizeInfo(sampleCount, UniformSize: 0, sizes);
    }

    private static async ValueTask<SampleSizeInfo> ReadCompactSampleSizeAsync (
        IsoBmffReader reader,
        BoxHeader box,
        CancellationToken cancellationToken)
    {
        var version = await ReadFullBoxVersionAsync(reader, box, cancellationToken).ConfigureAwait(false);
        if (version != 0)
        {
            throw new InvalidDataException($"stz2 uses unsupported version {version}.");
        }

        await reader.SkipAsync(3, box.End, cancellationToken).ConfigureAwait(false);
        var fieldSize = await reader.ReadByteAsync(box.End, cancellationToken).ConfigureAwait(false);
        if (fieldSize is not 4 and not 8 and not 16)
        {
            throw new InvalidDataException($"stz2 uses unsupported field_size {fieldSize}.");
        }

        var sampleCount = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
        var packedByteCount = fieldSize switch
        {
            4 => ((ulong)sampleCount + 1) / 2,
            8 => sampleCount,
            16 => (ulong)sampleCount * 2,
            _ => throw new InvalidDataException($"stz2 uses unsupported field_size {fieldSize}."),
        };
        RequireBoundedTableData(reader, box, sampleCount, packedByteCount, "stz2");
        var sizes = new List<uint>(checked((int)sampleCount));
        switch (fieldSize)
        {
            case 4:
                for (ulong sampleIndex = 0; sampleIndex < sampleCount; sampleIndex += 2)
                {
                    var packedSizes = await reader.ReadByteAsync(box.End, cancellationToken).ConfigureAwait(false);
                    sizes.Add(checked((uint)(packedSizes >> 4)));
                    if (sampleIndex + 1 < sampleCount)
                    {
                        sizes.Add(checked((uint)(packedSizes & 0x0F)));
                    }
                }

                break;
            case 8:
                for (uint sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    sizes.Add(await reader.ReadByteAsync(box.End, cancellationToken).ConfigureAwait(false));
                }

                break;
            case 16:
                for (uint sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    sizes.Add(await reader.ReadUInt16Async(box.End, cancellationToken).ConfigureAwait(false));
                }

                break;
        }

        await reader.RequireBoxDataEndAsync(box, cancellationToken).ConfigureAwait(false);
        return new SampleSizeInfo(sampleCount, UniformSize: 0, sizes);
    }

    private static async ValueTask<SampleToChunkInfo> ReadSampleToChunkAsync (
        IsoBmffReader reader,
        BoxHeader box,
        CancellationToken cancellationToken)
    {
        var version = await ReadFullBoxVersionAsync(reader, box, cancellationToken).ConfigureAwait(false);
        if (version != 0)
        {
            throw new InvalidDataException($"stsc uses unsupported version {version}.");
        }

        var entryCount = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
        RequireBoundedTableData(
            reader,
            box,
            entryCount,
            (ulong)entryCount * 12,
            "stsc");
        var entries = new List<SampleToChunkEntry>(checked((int)entryCount));
        uint previousFirstChunk = 0;
        for (uint entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            var firstChunk = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
            var samplesPerChunk = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
            var sampleDescriptionIndex = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
            if (firstChunk == 0 || firstChunk <= previousFirstChunk)
            {
                throw new InvalidDataException("stsc first_chunk values must be positive and strictly increasing.");
            }

            if (samplesPerChunk == 0 || sampleDescriptionIndex == 0)
            {
                throw new InvalidDataException(
                    "stsc samples_per_chunk and sample_description_index must be positive.");
            }

            entries.Add(new SampleToChunkEntry(firstChunk, samplesPerChunk, sampleDescriptionIndex));
            previousFirstChunk = firstChunk;
        }

        await reader.RequireBoxDataEndAsync(box, cancellationToken).ConfigureAwait(false);
        return new SampleToChunkInfo(entries);
    }

    private static async ValueTask<ChunkOffsetInfo> ReadChunkOffsetAsync (
        IsoBmffReader reader,
        BoxHeader box,
        bool uses64BitOffsets,
        CancellationToken cancellationToken)
    {
        var version = await ReadFullBoxVersionAsync(reader, box, cancellationToken).ConfigureAwait(false);
        if (version != 0)
        {
            var boxName = uses64BitOffsets ? "co64" : "stco";
            throw new InvalidDataException($"{boxName} uses unsupported version {version}.");
        }

        var entryCount = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
        var tableName = uses64BitOffsets ? "co64" : "stco";
        RequireBoundedTableData(
            reader,
            box,
            entryCount,
            (ulong)entryCount * (uses64BitOffsets ? 8UL : 4UL),
            tableName);
        var offsets = new List<ulong>(checked((int)entryCount));
        for (uint entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            offsets.Add(uses64BitOffsets
                ? await reader.ReadUInt64Async(box.End, cancellationToken).ConfigureAwait(false)
                : await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false));
        }

        await reader.RequireBoxDataEndAsync(box, cancellationToken).ConfigureAwait(false);
        return new ChunkOffsetInfo(offsets);
    }

    private static void RequireBoundedTableData (
        IsoBmffReader reader,
        BoxHeader box,
        uint declaredCount,
        ulong requiredByteCount,
        string tableName)
    {
        if (declaredCount > reader.MaximumSampleCount)
        {
            throw new InvalidDataException(
                $"{tableName} declares {declaredCount} entries, exceeding the supported structural limit "
                + $"of {reader.MaximumSampleCount} samples.");
        }

        reader.RequireRemainingBoxData(box, requiredByteCount, tableName);
    }

    private static async ValueTask<byte> ReadFullBoxVersionAsync (
        IsoBmffReader reader,
        BoxHeader box,
        CancellationToken cancellationToken)
    {
        var versionAndFlags = await reader.ReadUInt32Async(box.End, cancellationToken).ConfigureAwait(false);
        return checked((byte)(versionAndFlags >> 24));
    }

    private static async ValueTask<GameViewRecordingMp4ValidationResult> ValidateMovieAsync (
        IsoBmffReader reader,
        MovieInfo movie,
        ulong fileEnd,
        int expectedWidth,
        int expectedHeight,
        double expectedFrameRate,
        CancellationToken cancellationToken)
    {
        var videoTrack = movie.VideoTrack
            ?? throw new InvalidDataException("GameView recording MP4 must contain exactly one video track.");
        if (videoTrack.Header.Width != expectedWidth || videoTrack.Header.Height != expectedHeight)
        {
            throw new InvalidDataException(
                $"Video track dimensions do not match the recording request. Expected={expectedWidth}x{expectedHeight}, "
                + $"Actual={videoTrack.Header.Width}x{videoTrack.Header.Height}.");
        }

        var sampleTable = videoTrack.Media.SampleTable
            ?? throw new InvalidDataException("Video track does not contain a complete stbl box.");
        var sampleDescription = sampleTable.SampleDescription
            ?? throw new InvalidDataException("Video track does not contain a complete stsd box.");
        if (sampleDescription.EntryCount != 1 || sampleDescription.FirstEntry is not { } sampleEntry)
        {
            throw new InvalidDataException("Video track must contain exactly one sample description.");
        }

        if (sampleEntry.Type is not Avc1SampleEntryType and not Avc3SampleEntryType)
        {
            throw new InvalidDataException(
                $"Video track sample entry must use avc1 or avc3, not {FormatFourCc(sampleEntry.Type)}.");
        }

        if (sampleEntry.NalLengthSize is not { } nalLengthSize)
        {
            throw new InvalidDataException("AVC video sample entry does not contain a complete avcC box.");
        }

        if (sampleEntry.Width != expectedWidth || sampleEntry.Height != expectedHeight)
        {
            throw new InvalidDataException(
                $"Video sample entry dimensions do not match the recording request. Expected={expectedWidth}x{expectedHeight}, "
                + $"Actual={sampleEntry.Width}x{sampleEntry.Height}.");
        }

        var timeToSample = sampleTable.TimeToSample
            ?? throw new InvalidDataException("Video track does not contain a complete stts box.");
        if (timeToSample.SampleCount == 0 || timeToSample.SampleDelta is not { } sampleDelta)
        {
            throw new InvalidDataException("Finalized video track must describe at least one sample.");
        }


        await ValidateSampleDataAsync(
                reader,
                sampleTable,
                sampleDescription.EntryCount,
                timeToSample.SampleCount,
                fileEnd,
                nalLengthSize,
                cancellationToken)
            .ConfigureAwait(false);

        ulong sampleDuration;
        try
        {
            sampleDuration = checked(timeToSample.SampleCount * sampleDelta);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("Video sample duration exceeds the supported range.", exception);
        }

        if (!AreTickCountsEquivalent(videoTrack.Media.Header.Duration, sampleDuration))
        {
            throw new InvalidDataException(
                $"mdhd duration differs from the stts sample duration by more than one media-timescale tick. "
                + $"Expected={sampleDuration}, Actual={videoTrack.Media.Header.Duration}.");
        }

        var effectiveFrameRate = videoTrack.Media.Header.Timescale / (double)sampleDelta;
        if (!AreTimingValuesEquivalent(effectiveFrameRate, expectedFrameRate))
        {
            throw new InvalidDataException(
                $"Video sample interval does not match the requested frame rate. "
                + $"Expected={expectedFrameRate}, Actual={effectiveFrameRate}.");
        }

        var durationSeconds = videoTrack.Media.Header.Duration / (double)videoTrack.Media.Header.Timescale;
        var trackDurationSeconds = videoTrack.Header.Duration / (double)movie.Header.Timescale;
        if (!AreDurationValuesEquivalent(
                durationSeconds,
                trackDurationSeconds,
                videoTrack.Media.Header.Timescale,
                movie.Header.Timescale))
        {
            throw new InvalidDataException(
                $"Video track duration is inconsistent between tkhd and mdhd. "
                + $"TrackDuration={trackDurationSeconds}, MediaDuration={durationSeconds}.");
        }

        var movieDurationSeconds = movie.Header.Duration / (double)movie.Header.Timescale;
        if (!AreDurationValuesEquivalent(
                durationSeconds,
                movieDurationSeconds,
                videoTrack.Media.Header.Timescale,
                movie.Header.Timescale))
        {
            throw new InvalidDataException(
                $"Movie duration is inconsistent with the video track duration. "
                + $"MovieDuration={movieDurationSeconds}, VideoDuration={durationSeconds}.");
        }

        var durationFromRequestedFrameRate = timeToSample.SampleCount / expectedFrameRate;
        if (!AreDurationValuesEquivalent(
                durationSeconds,
                durationFromRequestedFrameRate,
                videoTrack.Media.Header.Timescale,
                videoTrack.Media.Header.Timescale))
        {
            throw new InvalidDataException(
                $"Video duration is inconsistent with sample count and requested frame rate. "
                + $"Duration={durationSeconds}, Samples={timeToSample.SampleCount}, FrameRate={expectedFrameRate}.");
        }

        return new GameViewRecordingMp4ValidationResult(
            FormatFourCc(sampleEntry.Type),
            new PixelDimensions(expectedWidth, expectedHeight),
            movie.Header.Timescale,
            movie.Header.Duration,
            videoTrack.Media.Header.Timescale,
            sampleDelta,
            timeToSample.SampleCount,
            sampleDuration,
            durationSeconds,
            effectiveFrameRate);
    }

    private static async ValueTask ValidateSampleDataAsync (
        IsoBmffReader reader,
        SampleTableInfo sampleTable,
        uint sampleDescriptionCount,
        ulong expectedSampleCount,
        ulong fileEnd,
        int nalLengthSize,
        CancellationToken cancellationToken)
    {
        var sampleSizes = sampleTable.SampleSize
            ?? throw new InvalidDataException("Video track does not contain a complete stsz or stz2 box.");
        if (sampleSizes.SampleCount != expectedSampleCount)
        {
            throw new InvalidDataException(
                $"Video sample count is inconsistent between sample-size and timing tables. "
                + $"SampleSizes={sampleSizes.SampleCount}, Timing={expectedSampleCount}.");
        }

        var sampleToChunk = sampleTable.SampleToChunk
            ?? throw new InvalidDataException("Video track does not contain a complete stsc box.");
        if (sampleToChunk.Entries.Count == 0 || sampleToChunk.Entries[0].FirstChunk != 1)
        {
            throw new InvalidDataException("Video stsc must begin its chunk mapping at chunk 1.");
        }

        var chunkOffsets = sampleTable.ChunkOffset
            ?? throw new InvalidDataException("Video track does not contain a complete stco or co64 box.");
        if (chunkOffsets.Offsets.Count == 0)
        {
            throw new InvalidDataException("Finalized video track must describe at least one chunk.");
        }

        if (sampleToChunk.Entries[^1].FirstChunk > chunkOffsets.Offsets.Count)
        {
            throw new InvalidDataException("Video stsc refers to a chunk that has no chunk offset.");
        }

        ulong sampleIndex = 0;
        var sampleToChunkEntryIndex = 0;
        for (var chunkIndex = 0; chunkIndex < chunkOffsets.Offsets.Count; chunkIndex++)
        {
            var oneBasedChunkIndex = checked((uint)chunkIndex + 1);
            if (sampleToChunkEntryIndex + 1 < sampleToChunk.Entries.Count
                && sampleToChunk.Entries[sampleToChunkEntryIndex + 1].FirstChunk == oneBasedChunkIndex)
            {
                sampleToChunkEntryIndex++;
            }

            var mapping = sampleToChunk.Entries[sampleToChunkEntryIndex];
            if (mapping.SampleDescriptionIndex > sampleDescriptionCount)
            {
                throw new InvalidDataException(
                    "Video stsc refers to a sample description that does not exist.");
            }

            var firstSampleIndex = sampleIndex;
            ulong chunkSize = 0;
            for (uint sampleInChunk = 0; sampleInChunk < mapping.SamplesPerChunk; sampleInChunk++)
            {
                if (sampleIndex >= sampleSizes.SampleCount)
                {
                    throw new InvalidDataException("Video chunk mapping declares more samples than stsz or stz2.");
                }

                var sampleSize = sampleSizes.GetSize(sampleIndex);
                if (sampleSize == 0)
                {
                    throw new InvalidDataException("Finalized video samples must not be empty.");
                }

                try
                {
                    chunkSize = checked(chunkSize + sampleSize);
                    sampleIndex = checked(sampleIndex + 1);
                }
                catch (OverflowException exception)
                {
                    throw new InvalidDataException("Video sample byte range exceeds the supported range.", exception);
                }
            }

            var chunkStart = chunkOffsets.Offsets[chunkIndex];
            ulong chunkEnd;
            try
            {
                chunkEnd = checked(chunkStart + chunkSize);
            }
            catch (OverflowException exception)
            {
                throw new InvalidDataException("Video chunk byte range exceeds the supported range.", exception);
            }

            if (!await IsRangeWithinMediaDataAsync(
                    reader,
                    chunkStart,
                    chunkEnd,
                    fileEnd,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                throw new InvalidDataException(
                    $"Video chunk {oneBasedChunkIndex} is not fully backed by an mdat payload.");
            }

            reader.Seek(chunkStart);
            for (var validationSampleIndex = firstSampleIndex;
                validationSampleIndex < sampleIndex;
                validationSampleIndex++)
            {
                await ValidateAvcSampleAsync(
                        reader,
                        sampleSizes.GetSize(validationSampleIndex),
                        nalLengthSize,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (sampleIndex != sampleSizes.SampleCount)
        {
            throw new InvalidDataException(
                $"Video chunk mapping does not account for every declared sample. "
                + $"Mapped={sampleIndex}, Declared={sampleSizes.SampleCount}.");
        }
    }

    private static async ValueTask ValidateAvcSampleAsync (
        IsoBmffReader reader,
        uint sampleSize,
        int nalLengthSize,
        CancellationToken cancellationToken)
    {
        ulong sampleEnd;
        try
        {
            sampleEnd = checked(reader.Position + sampleSize);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("AVC sample byte range exceeds the supported range.", exception);
        }

        var foundNalUnit = false;
        var foundVideoCodingLayerNalUnit = false;
        while (reader.Position < sampleEnd)
        {
            if ((ulong)nalLengthSize > sampleEnd - reader.Position)
            {
                throw new InvalidDataException("AVC sample ends within a NAL-unit length field.");
            }

            var nalUnitSize = await reader
                .ReadBigEndianUInt32Async(nalLengthSize, sampleEnd, cancellationToken)
                .ConfigureAwait(false);
            if (nalUnitSize <= 1 || nalUnitSize > sampleEnd - reader.Position)
            {
                throw new InvalidDataException(
                    "AVC sample contains a header-only, empty, or out-of-bounds NAL unit.");
            }

            var nalHeader = await reader.ReadByteAsync(sampleEnd, cancellationToken).ConfigureAwait(false);
            var nalUnitType = checked((byte)(nalHeader & 0x1F));
            if ((nalHeader & 0x80) != 0 || !IsKnownAvcNalUnitType(nalUnitType))
            {
                throw new InvalidDataException(
                    $"AVC sample contains an invalid NAL-unit header. Type={nalUnitType}.");
            }

            await reader.SkipAsync(nalUnitSize - 1, sampleEnd, cancellationToken).ConfigureAwait(false);
            foundNalUnit = true;
            foundVideoCodingLayerNalUnit |= nalUnitType is >= 1 and <= 5;
        }

        if (!foundNalUnit)
        {
            throw new InvalidDataException("Finalized AVC samples must contain at least one NAL unit.");
        }
        if (!foundVideoCodingLayerNalUnit)
        {
            throw new InvalidDataException(
                "Every finalized AVC sample must contain a video-coding-layer NAL unit.");
        }
    }

    private static async ValueTask<bool> IsRangeWithinMediaDataAsync (
        IsoBmffReader reader,
        ulong rangeStart,
        ulong rangeEnd,
        ulong fileEnd,
        CancellationToken cancellationToken)
    {
        reader.Seek(0);
        while (reader.Position < fileEnd)
        {
            var box = await reader.TryReadBoxHeaderAsync(parentEnd: null, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("MP4 ended while locating its mdat payloads.");
            var boxEnd = box.End ?? fileEnd;
            if (boxEnd > fileEnd)
            {
                throw new InvalidDataException("Top-level ISO-BMFF box exceeds the validated file boundary.");
            }

            if (box.Type == MediaDataBoxType
                && rangeEnd > rangeStart
                && rangeStart >= reader.Position
                && rangeEnd <= boxEnd)
            {
                return true;
            }

            reader.Seek(boxEnd);
        }

        return false;
    }

    private static bool IsKnownAvcNalUnitType (byte nalUnitType)
    {
        return nalUnitType is (>= 1 and <= 15) or (>= 19 and <= 21);
    }

    private static int ReadFixedPointDimension (
        uint rawValue,
        string fieldName)
    {
        if ((rawValue & ushort.MaxValue) != 0)
        {
            throw new InvalidDataException($"{fieldName} must use an integral 16.16 value.");
        }

        return checked((int)(rawValue >> 16));
    }

    private static void ValidateExpectedDimension (
        int dimension,
        string parameterName)
    {
        if (dimension is <= 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                dimension,
                $"MP4 video dimension must be between 1 and {ushort.MaxValue}.");
        }
    }

    private static bool AreTimingValuesEquivalent (
        double left,
        double right)
    {
        var scale = Math.Max(Math.Abs(left), Math.Abs(right));
        return Math.Abs(left - right) <= (scale * RelativeTimingTolerance);
    }

    private static bool AreDurationValuesEquivalent (
        double left,
        double right,
        uint leftTimescale,
        uint rightTimescale)
    {
        var quantizationTolerance = Math.Max(1d / leftTimescale, 1d / rightTimescale);
        var relativeTolerance = Math.Max(Math.Abs(left), Math.Abs(right)) * RelativeTimingTolerance;
        return Math.Abs(left - right) <= Math.Max(quantizationTolerance, relativeTolerance);
    }

    private static bool AreTickCountsEquivalent (ulong left, ulong right) =>
        left >= right ? left - right <= 1 : right - left <= 1;

    private static string FormatFourCc (uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return Encoding.ASCII.GetString(bytes);
    }

    private readonly record struct BoxHeader (
        uint Type,
        ulong? End);

    private sealed record MovieInfo (
        MovieHeaderInfo Header,
        TrackInfo? VideoTrack);

    private readonly record struct MovieHeaderInfo (
        uint Timescale,
        ulong Duration);

    private readonly record struct TrackInfo (
        TrackHeaderInfo Header,
        MediaInfo Media);

    private readonly record struct TrackHeaderInfo (
        int Width,
        int Height,
        ulong Duration);

    private readonly record struct SequenceParameterSetInfo (
        uint Id,
        byte Profile,
        byte ProfileCompatibility,
        byte Level,
        uint ChromaFormatIdc,
        int Width,
        int Height);

    private readonly record struct PictureParameterSetInfo (
        uint Id,
        uint SequenceParameterSetId);

    private readonly record struct MediaInfo (
        MediaHeaderInfo Header,
        uint HandlerType,
        SampleTableInfo? SampleTable);

    private readonly record struct MediaHeaderInfo (
        uint Timescale,
        ulong Duration);

    private readonly record struct SampleTableInfo (
        SampleDescriptionInfo? SampleDescription,
        TimeToSampleInfo? TimeToSample,
        SampleSizeInfo? SampleSize,
        SampleToChunkInfo? SampleToChunk,
        ChunkOffsetInfo? ChunkOffset);

    private readonly record struct SampleDescriptionInfo (
        uint EntryCount,
        SampleEntryInfo? FirstEntry);

    private readonly record struct SampleEntryInfo (
        uint Type,
        int? Width,
        int? Height,
        int? NalLengthSize);

    private readonly record struct TimeToSampleInfo (
        ulong SampleCount,
        uint? SampleDelta);

    private readonly record struct SampleSizeInfo (
        uint SampleCount,
        uint UniformSize,
        IReadOnlyList<uint>? Sizes)
    {
        public uint GetSize (ulong sampleIndex)
        {
            if (sampleIndex >= SampleCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleIndex),
                    sampleIndex,
                    "Sample index must identify a declared sample.");
            }

            return UniformSize != 0
                ? UniformSize
                : Sizes![checked((int)sampleIndex)];
        }
    }

    private readonly record struct SampleToChunkInfo (
        IReadOnlyList<SampleToChunkEntry> Entries);

    private readonly record struct SampleToChunkEntry (
        uint FirstChunk,
        uint SamplesPerChunk,
        uint SampleDescriptionIndex);

    private readonly record struct ChunkOffsetInfo (
        IReadOnlyList<ulong> Offsets);

    private sealed class AvcBitReader
    {
        private readonly byte[] bytes;

        private int bitOffset;

        public AvcBitReader (byte[] bytes)
        {
            this.bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        }

        public int BitsRemaining => checked((bytes.Length * 8) - bitOffset);

        public bool ReadFlag () => ReadBits(1) != 0;

        public uint ReadBits (int count)
        {
            if (count is < 1 or > 32 || count > BitsRemaining)
            {
                throw new InvalidDataException("AVC parameter set is truncated within a bit field.");
            }

            uint value = 0;
            for (var index = 0; index < count; index++)
            {
                value = (value << 1) | checked((uint)PeekBit(0));
                bitOffset++;
            }

            return value;
        }

        public uint ReadUnsignedExpGolomb (uint maximumValue)
        {
            var leadingZeroCount = 0;
            while (ReadBits(1) == 0)
            {
                leadingZeroCount++;
                if (leadingZeroCount >= 32)
                {
                    throw new InvalidDataException("AVC exponential-Golomb value exceeds the supported range.");
                }
            }

            var suffix = leadingZeroCount == 0 ? 0 : ReadBits(leadingZeroCount);
            var value = checked(((1u << leadingZeroCount) - 1) + suffix);
            if (value > maximumValue)
            {
                throw new InvalidDataException("AVC exponential-Golomb value exceeds its syntax range.");
            }

            return value;
        }

        public int ReadSignedExpGolomb ()
        {
            var code = ReadUnsignedExpGolomb(int.MaxValue);
            return (code & 1) == 0
                ? -checked((int)(code / 2))
                : checked((int)((code + 1) / 2));
        }

        public bool MoreRbspData ()
        {
            if (BitsRemaining == 0)
            {
                throw new InvalidDataException("AVC parameter set does not contain rbsp_trailing_bits.");
            }
            if (BitsRemaining > 8 || PeekBit(0) == 0)
            {
                return true;
            }

            for (var offset = 1; offset < BitsRemaining; offset++)
            {
                if (PeekBit(offset) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        public void ReadRbspTrailingBits ()
        {
            if (BitsRemaining is < 1 or > 8 || !ReadFlag())
            {
                throw new InvalidDataException("AVC parameter set has invalid rbsp_trailing_bits.");
            }

            while (BitsRemaining != 0)
            {
                if (ReadFlag())
                {
                    throw new InvalidDataException("AVC parameter set has non-zero rbsp alignment bits.");
                }
            }
        }

        private int PeekBit (int relativeOffset)
        {
            var absoluteOffset = checked(bitOffset + relativeOffset);
            if (absoluteOffset < 0 || absoluteOffset >= bytes.Length * 8)
            {
                throw new InvalidDataException("AVC parameter set is truncated.");
            }

            var value = bytes[absoluteOffset / 8];
            return (value >> (7 - (absoluteOffset % 8))) & 1;
        }
    }

    private sealed class IsoBmffReader
    {
        private readonly byte[] discardBuffer = new byte[8192];
        private readonly byte[] headerBuffer = new byte[8];
        private readonly byte[] scalarBuffer = new byte[8];
        private readonly Stream stream;

        private bool reachedEndOfStream;

        public IsoBmffReader (
            Stream stream,
            uint maximumSampleCount)
        {
            this.stream = stream;
            MaximumSampleCount = maximumSampleCount;
        }

        public uint MaximumSampleCount { get; }

        public ulong Position { get; private set; }

        public void Seek (ulong position)
        {
            if (position > long.MaxValue)
            {
                throw new InvalidDataException("ISO-BMFF offset exceeds the seekable stream range.");
            }

            var actualPosition = stream.Seek(checked((long)position), SeekOrigin.Begin);
            if (actualPosition != checked((long)position))
            {
                throw new InvalidDataException("MP4 stream did not seek to the requested ISO-BMFF offset.");
            }

            Position = position;
            reachedEndOfStream = false;
        }

        public async ValueTask<BoxHeader?> TryReadBoxHeaderAsync (
            ulong? parentEnd,
            CancellationToken cancellationToken)
        {
            EnsurePositionWithin(parentEnd);
            if (parentEnd == Position)
            {
                return null;
            }

            var start = Position;
            if (!await TryReadFirstHeaderByteAsync(parentEnd, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            await ReadExactlyAsync(
                    headerBuffer.AsMemory(1, 7),
                    parentEnd,
                    "ISO-BMFF box header is truncated.",
                    cancellationToken)
                .ConfigureAwait(false);
            var size32 = BinaryPrimitives.ReadUInt32BigEndian(headerBuffer.AsSpan(0, 4));
            var type = BinaryPrimitives.ReadUInt32BigEndian(headerBuffer.AsSpan(4, 4));
            ulong headerSize = 8;
            ulong? end;
            switch (size32)
            {
                case 0:
                    end = parentEnd;
                    break;
                case 1:
                    var extendedSize = await ReadUInt64Async(parentEnd, cancellationToken).ConfigureAwait(false);
                    headerSize = 16;
                    if (extendedSize < headerSize)
                    {
                        throw new InvalidDataException("Extended ISO-BMFF box size is smaller than its header.");
                    }

                    end = AddBoxSize(start, extendedSize);
                    break;
                default:
                    if (size32 < headerSize)
                    {
                        throw new InvalidDataException("ISO-BMFF box size is smaller than its header.");
                    }

                    end = AddBoxSize(start, size32);
                    break;
            }

            if (end is { } finiteEnd)
            {
                if (finiteEnd < Position)
                {
                    throw new InvalidDataException("ISO-BMFF box ends before its complete header.");
                }

                if (parentEnd is { } finiteParentEnd && finiteEnd > finiteParentEnd)
                {
                    throw new InvalidDataException("Nested ISO-BMFF box exceeds its parent boundary.");
                }
            }

            return new BoxHeader(type, end);
        }

        public async ValueTask<uint> ReadUInt32Async (
            ulong? boundary,
            CancellationToken cancellationToken)
        {
            await ReadExactlyAsync(
                    scalarBuffer.AsMemory(0, 4),
                    boundary,
                    "ISO-BMFF uint32 field is truncated.",
                    cancellationToken)
                .ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt32BigEndian(scalarBuffer.AsSpan(0, 4));
        }

        public async ValueTask<uint> ReadBigEndianUInt32Async (
            int byteCount,
            ulong boundary,
            CancellationToken cancellationToken)
        {
            if (byteCount is not 1 and not 2 and not 4)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(byteCount),
                    byteCount,
                    "Big-endian integer width must be 1, 2, or 4 bytes.");
            }

            uint value = 0;
            for (var index = 0; index < byteCount; index++)
            {
                value = (value << 8)
                    | await ReadByteAsync(boundary, cancellationToken).ConfigureAwait(false);
            }

            return value;
        }

        public async ValueTask<ulong> ReadUInt64Async (
            ulong? boundary,
            CancellationToken cancellationToken)
        {
            await ReadExactlyAsync(
                    scalarBuffer.AsMemory(0, 8),
                    boundary,
                    "ISO-BMFF uint64 field is truncated.",
                    cancellationToken)
                .ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt64BigEndian(scalarBuffer);
        }

        public async ValueTask<ushort> ReadUInt16Async (
            ulong? boundary,
            CancellationToken cancellationToken)
        {
            await ReadExactlyAsync(
                    scalarBuffer.AsMemory(0, 2),
                    boundary,
                    "ISO-BMFF uint16 field is truncated.",
                    cancellationToken)
                .ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt16BigEndian(scalarBuffer.AsSpan(0, 2));
        }

        public async ValueTask<byte> ReadByteAsync (
            ulong? boundary,
            CancellationToken cancellationToken)
        {
            await ReadExactlyAsync(
                    scalarBuffer.AsMemory(0, 1),
                    boundary,
                    "ISO-BMFF byte field is truncated.",
                    cancellationToken)
                .ConfigureAwait(false);
            return scalarBuffer[0];
        }

        public async ValueTask SkipAsync (
            ulong byteCount,
            ulong? boundary,
            CancellationToken cancellationToken)
        {
            EnsureAvailableWithinBoundary(byteCount, boundary);
            var remaining = byteCount;
            while (remaining != 0)
            {
                var requested = checked((int)Math.Min((ulong)discardBuffer.Length, remaining));
                var bytesRead = await stream
                    .ReadAsync(discardBuffer.AsMemory(0, requested), cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    reachedEndOfStream = true;
                    throw new InvalidDataException("ISO-BMFF box data is truncated.");
                }

                Advance(checked((uint)bytesRead));
                remaining -= checked((uint)bytesRead);
            }
        }

        public async ValueTask SkipBoxAsync (
            BoxHeader box,
            CancellationToken cancellationToken)
        {
            if (box.End is { } end)
            {
                EnsurePositionWithin(end);
                await SkipAsync(end - Position, end, cancellationToken).ConfigureAwait(false);
                return;
            }

            await DrainToEndOfStreamAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask EnsureBoxConsumedAsync (
            BoxHeader box,
            CancellationToken cancellationToken)
        {
            if (box.End is { } end)
            {
                if (Position != end)
                {
                    throw new InvalidDataException("ISO-BMFF box parser did not consume its declared size.");
                }

                return;
            }

            if (!reachedEndOfStream)
            {
                await RequireEndOfStreamAsync(
                        "Size-zero ISO-BMFF box did not consume the remaining stream.",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async ValueTask RequireBoxDataEndAsync (
            BoxHeader box,
            CancellationToken cancellationToken)
        {
            if (box.End is { } end)
            {
                if (Position != end)
                {
                    throw new InvalidDataException("ISO-BMFF box contains unexpected trailing data.");
                }

                return;
            }

            await RequireEndOfStreamAsync(
                    "Size-zero ISO-BMFF box contains unexpected trailing data.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public void RequireRemainingBoxData (
            BoxHeader box,
            ulong requiredByteCount,
            string tableName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
            var end = box.End
                ?? throw new InvalidDataException($"{tableName} must declare a finite box size.");
            EnsurePositionWithin(end);
            if (requiredByteCount > end - Position)
            {
                throw new InvalidDataException(
                    $"{tableName} does not contain its declared table entries.");
            }
        }

        private static ulong AddBoxSize (
            ulong start,
            ulong size)
        {
            try
            {
                return checked(start + size);
            }
            catch (OverflowException exception)
            {
                throw new InvalidDataException("ISO-BMFF box end overflows the supported offset range.", exception);
            }
        }

        private async ValueTask<bool> TryReadFirstHeaderByteAsync (
            ulong? boundary,
            CancellationToken cancellationToken)
        {
            EnsureAvailableWithinBoundary(1, boundary);
            var bytesRead = await stream
                .ReadAsync(headerBuffer.AsMemory(0, 1), cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead == 0)
            {
                reachedEndOfStream = true;
                if (boundary is not null)
                {
                    throw new InvalidDataException("ISO-BMFF container is truncated before its declared end.");
                }

                return false;
            }

            Advance(1);
            return true;
        }

        private async ValueTask ReadExactlyAsync (
            Memory<byte> buffer,
            ulong? boundary,
            string failureMessage,
            CancellationToken cancellationToken)
        {
            EnsureAvailableWithinBoundary(checked((ulong)buffer.Length), boundary);
            var offset = 0;
            while (offset < buffer.Length)
            {
                var bytesRead = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    reachedEndOfStream = true;
                    throw new InvalidDataException(failureMessage);
                }

                Advance(checked((uint)bytesRead));
                offset += bytesRead;
            }
        }

        private async ValueTask DrainToEndOfStreamAsync (CancellationToken cancellationToken)
        {
            while (true)
            {
                var bytesRead = await stream.ReadAsync(discardBuffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    reachedEndOfStream = true;
                    return;
                }

                Advance(checked((uint)bytesRead));
            }
        }

        private async ValueTask RequireEndOfStreamAsync (
            string failureMessage,
            CancellationToken cancellationToken)
        {
            if (reachedEndOfStream)
            {
                return;
            }

            var bytesRead = await stream
                .ReadAsync(discardBuffer.AsMemory(0, 1), cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead == 0)
            {
                reachedEndOfStream = true;
                return;
            }

            Advance(1);
            throw new InvalidDataException(failureMessage);
        }

        private void EnsureAvailableWithinBoundary (
            ulong byteCount,
            ulong? boundary)
        {
            EnsurePositionWithin(boundary);
            if (boundary is { } end && byteCount > (end - Position))
            {
                throw new InvalidDataException("ISO-BMFF field exceeds its containing box boundary.");
            }
        }

        private void EnsurePositionWithin (ulong? boundary)
        {
            if (boundary is { } end && Position > end)
            {
                throw new InvalidDataException("ISO-BMFF reader advanced beyond its containing box boundary.");
            }
        }

        private void Advance (uint byteCount)
        {
            try
            {
                Position = checked(Position + byteCount);
            }
            catch (OverflowException exception)
            {
                throw new InvalidDataException("ISO-BMFF stream offset exceeds the supported range.", exception);
            }
        }
    }
}
