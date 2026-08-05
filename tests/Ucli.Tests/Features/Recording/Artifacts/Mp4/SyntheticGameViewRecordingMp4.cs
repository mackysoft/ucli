using System.Buffers.Binary;
using System.Text;

namespace MackySoft.Ucli.Tests.Features.Recording.Artifacts.Mp4;

internal static class SyntheticGameViewRecordingMp4
{
    public const int Height = 240;
    public const uint EncodedSampleSize = 248;
    public const uint MediaTimescale = 30000;
    public const uint SampleDelta = 1000;
    public const uint SampleCount = 60;
    public const int Width = 320;

    private static readonly byte[] RecorderEncodedSample = Convert.FromHexString(
        "000000F46588804BFFFFF04514000EF8E0006A380011BD5F7DF7DF7DF7DF7DF7"
        + "DF7DF7DF7D6ABAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEB"
        + "AEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBA"
        + "EBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAE"
        + "BAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEB"
        + "AEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBA"
        + "EBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAE"
        + "BAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBAEBC0");

    public static byte[] Create (
        string codec = "avc1",
        int trackWidth = Width,
        int trackHeight = Height,
        int sampleEntryWidth = Width,
        int sampleEntryHeight = Height,
        IReadOnlyList<(uint SampleCount, uint SampleDelta)>? timeToSampleEntries = null,
        ulong? mediaDurationOverride = null,
        ulong? trackDurationOverride = null,
        ulong? movieDurationOverride = null,
        bool includeAudioTrack = false,
        int videoTrackCount = 1,
        bool includeMediaData = true,
        bool useCompactSampleSizes = false,
        bool use64BitChunkOffsets = false)
    {
        return CreateCore(
            codec,
            trackWidth,
            trackHeight,
            sampleEntryWidth,
            sampleEntryHeight,
            timeToSampleEntries,
            mediaDurationOverride,
            trackDurationOverride,
            movieDurationOverride,
            includeAudioTrack,
            videoTrackCount,
            includeMediaData,
            useCompactSampleSizes,
            use64BitChunkOffsets,
            includeAvcConfiguration: true,
            EncodedSampleSize,
            samplesPerChunkOverride: null,
            chunkOffsetOverride: null);
    }

    public static byte[] CreateWithoutAvcConfiguration ()
    {
        return CreateCore(
            includeAvcConfiguration: false);
    }

    public static byte[] CreateWithInvalidAvcConfiguration ()
    {
        var bytes = Create();
        var payloadOffset = FindBoxType(bytes, "avcC") + 4;
        bytes[payloadOffset] = 0;
        return bytes;
    }

    public static byte[] CreateWithEmptyDeclaredSamples (bool useCompactSampleSizes = false)
    {
        return CreateCore(
            useCompactSampleSizes: useCompactSampleSizes,
            sampleSize: 0);
    }

    public static byte[] CreateWithUnmappedSample ()
    {
        return CreateCore(
            samplesPerChunkOverride: SampleCount - 1);
    }

    public static byte[] CreateWithEmptyNalUnit ()
    {
        var bytes = Create();
        var payloadOffset = FindBoxType(bytes, "mdat") + 4;
        bytes.AsSpan(payloadOffset).Clear();
        return bytes;
    }

    public static byte[] CreateWithInvalidNalUnitType ()
    {
        var bytes = Create();
        var payloadOffset = FindBoxType(bytes, "mdat") + 4;
        bytes[payloadOffset + 4] = 0;
        return bytes;
    }

    public static byte[] CreateWithAudOnlySamples ()
    {
        byte[] sample = [0, 0, 0, 2, 0x09, 0x10];
        return CreateCore(sampleSize: checked((uint)sample.Length), samplePayload: sample);
    }

    public static byte[] CreateWithVclHeaderOnlySamples ()
    {
        byte[] sample = [0, 0, 0, 1, 0x65];
        return CreateCore(sampleSize: checked((uint)sample.Length), samplePayload: sample);
    }

    public static byte[] CreateWithMissingSequenceParameterSets ()
    {
        var bytes = Create();
        var payloadOffset = FindBoxType(bytes, "avcC") + 4;
        bytes[payloadOffset + 5] = 0xE0;
        return bytes;
    }

    public static byte[] CreateWithMissingPictureParameterSets ()
    {
        var bytes = Create();
        var payloadOffset = FindBoxType(bytes, "avcC") + 4;
        bytes[payloadOffset + 33] = 0;
        return bytes;
    }

    public static byte[] CreateWithMismatchedAvcProfile ()
    {
        var bytes = Create();
        var payloadOffset = FindBoxType(bytes, "avcC") + 4;
        bytes[payloadOffset + 1] = 77;
        return bytes;
    }

    public static byte[] CreateWithTruncatedSequenceParameterSet ()
    {
        var bytes = Create();
        var payloadOffset = FindBoxType(bytes, "avcC") + 4;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(payloadOffset + 6, 2), 2);
        return bytes;
    }

    public static byte[] CreateWithoutFileType ()
    {
        var bytes = Create();
        var fileTypeSize = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(0, 4)));
        return bytes[fileTypeSize..];
    }

    public static byte[] CreateWithDuplicateFileType ()
    {
        return Concatenate([CreateFileType(), Create()]);
    }

    public static byte[] CreateWithIncompatibleFileType ()
    {
        var bytes = Create();
        var payloadOffset = FindBoxType(bytes, "ftyp") + 4;
        WriteFourCc(bytes.AsSpan(payloadOffset, 4), "zzzz");
        WriteFourCc(bytes.AsSpan(payloadOffset + 8, 4), "zzzz");
        WriteFourCc(bytes.AsSpan(payloadOffset + 12, 4), "zzzz");
        return bytes;
    }

    public static byte[] CreateWithNonIdentityTrackMatrix ()
    {
        var bytes = Create();
        var matrixOffset = FindBoxType(bytes, "tkhd") + 44;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(matrixOffset, 4), 0);
        return bytes;
    }

    public static byte[] CreateWithEditList ()
    {
        return CreateCore(includeEditList: true);
    }

    public static byte[] CreateWithCompositionTimeOffsets ()
    {
        return CreateCore(includeCompositionTimeOffsets: true);
    }

    public static byte[] CreateWithChunkOutsideMediaData (bool use64BitChunkOffsets = false)
    {
        return CreateCore(
            use64BitChunkOffsets: use64BitChunkOffsets,
            chunkOffsetOverride: use64BitChunkOffsets ? ulong.MaxValue : uint.MaxValue);
    }

    private static byte[] CreateCore (
        string codec = "avc1",
        int trackWidth = Width,
        int trackHeight = Height,
        int sampleEntryWidth = Width,
        int sampleEntryHeight = Height,
        IReadOnlyList<(uint SampleCount, uint SampleDelta)>? timeToSampleEntries = null,
        ulong? mediaDurationOverride = null,
        ulong? trackDurationOverride = null,
        ulong? movieDurationOverride = null,
        bool includeAudioTrack = false,
        int videoTrackCount = 1,
        bool includeMediaData = true,
        bool useCompactSampleSizes = false,
        bool use64BitChunkOffsets = false,
        bool includeAvcConfiguration = true,
        uint sampleSize = EncodedSampleSize,
        uint? samplesPerChunkOverride = null,
        ulong? chunkOffsetOverride = null,
        bool includeEditList = false,
        bool includeCompositionTimeOffsets = false,
        byte[]? samplePayload = null)
    {
        timeToSampleEntries ??= [(SampleCount, SampleDelta)];
        var calculatedDuration = CalculateDuration(timeToSampleEntries);
        var movieDuration = movieDurationOverride ?? (calculatedDuration == 0 ? SampleDelta : calculatedDuration);
        var sampleCount = checked((uint)timeToSampleEntries.Aggregate(
            0UL,
            static (total, entry) => checked(total + entry.SampleCount)));
        var fileType = CreateFileType();
        var free = Box("free", [0x01, 0x02], extendedSize: true);

        var placeholderMovie = CreateMovie(
            codec,
            trackWidth,
            trackHeight,
            sampleEntryWidth,
            sampleEntryHeight,
            timeToSampleEntries,
            mediaDurationOverride,
            trackDurationOverride,
            movieDuration,
            includeAudioTrack,
            videoTrackCount,
            useCompactSampleSizes,
            use64BitChunkOffsets,
            includeAvcConfiguration,
            sampleCount,
            sampleSize,
            samplesPerChunkOverride,
            chunkOffset: 0,
            includeEditList,
            includeCompositionTimeOffsets);
        var calculatedChunkOffset = checked(
            (ulong)fileType.Length
            + (ulong)free.Length
            + (ulong)placeholderMovie.Length
            + 8);
        var movie = CreateMovie(
            codec,
            trackWidth,
            trackHeight,
            sampleEntryWidth,
            sampleEntryHeight,
            timeToSampleEntries,
            mediaDurationOverride,
            trackDurationOverride,
            movieDuration,
            includeAudioTrack,
            videoTrackCount,
            useCompactSampleSizes,
            use64BitChunkOffsets,
            includeAvcConfiguration,
            sampleCount,
            sampleSize,
            samplesPerChunkOverride,
            chunkOffsetOverride ?? calculatedChunkOffset,
            includeEditList,
            includeCompositionTimeOffsets);

        var files = new List<byte[]>
        {
            fileType,
            free,
            movie,
        };
        if (includeMediaData)
        {
            files.Add(SizeZeroBox("mdat", CreateMediaData(sampleCount, sampleSize, samplePayload)));
        }

        return Concatenate(files);
    }

    private static byte[] CreateMovie (
        string codec,
        int trackWidth,
        int trackHeight,
        int sampleEntryWidth,
        int sampleEntryHeight,
        IReadOnlyList<(uint SampleCount, uint SampleDelta)> timeToSampleEntries,
        ulong? mediaDurationOverride,
        ulong? trackDurationOverride,
        ulong movieDuration,
        bool includeAudioTrack,
        int videoTrackCount,
        bool useCompactSampleSizes,
        bool use64BitChunkOffsets,
        bool includeAvcConfiguration,
        uint sampleCount,
        uint sampleSize,
        uint? samplesPerChunkOverride,
        ulong chunkOffset,
        bool includeEditList,
        bool includeCompositionTimeOffsets)
    {
        var calculatedDuration = CalculateDuration(timeToSampleEntries);
        var tracks = new List<byte[]>();
        for (var trackIndex = 0; trackIndex < videoTrackCount; trackIndex++)
        {
            tracks.Add(CreateTrack(
                trackId: checked((uint)trackIndex + 1),
                handlerType: "vide",
                codec,
                trackWidth,
                trackHeight,
                sampleEntryWidth,
                sampleEntryHeight,
                timeToSampleEntries,
                mediaDurationOverride ?? calculatedDuration,
                trackDurationOverride ?? calculatedDuration,
                includeAvcConfiguration,
                useCompactSampleSizes,
                use64BitChunkOffsets,
                sampleCount,
                sampleSize,
                samplesPerChunkOverride ?? sampleCount,
                chunkOffset,
                includeEditList,
                includeCompositionTimeOffsets));
        }

        if (includeAudioTrack)
        {
            tracks.Add(CreateTrack(
                trackId: checked((uint)tracks.Count + 1),
                handlerType: "soun",
                codec: "mp4a",
                trackWidth: 0,
                trackHeight: 0,
                sampleEntryWidth: 0,
                sampleEntryHeight: 0,
                timeToSampleEntries,
                calculatedDuration,
                calculatedDuration,
                includeAvcConfiguration: false,
                useCompactSampleSizes: false,
                use64BitChunkOffsets,
                sampleCount,
                sampleSize,
                samplesPerChunkOverride ?? sampleCount,
                chunkOffset,
                includeEditList: false,
                includeCompositionTimeOffsets: false));
        }

        var moviePayloadParts = new List<byte[]>
        {
            CreateMovieHeader(movieDuration),
            Box("free", [0x42, 0x4F, 0x58]),
        };
        moviePayloadParts.AddRange(tracks);
        return Box("moov", Concatenate(moviePayloadParts), extendedSize: true);
    }

    public static byte[] CreateExtendedSizeOverflow ()
    {
        return Concatenate(
        [
            Box("free", []),
            ExtendedBoxHeader("free", ulong.MaxValue),
        ]);
    }

    public static byte[] CreateNestedBoxOutsideParent ()
    {
        var oversizedChildHeader = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(oversizedChildHeader.AsSpan(0, 4), 100);
        WriteFourCc(oversizedChildHeader.AsSpan(4, 4), "free");
        return Box("moov", oversizedChildHeader);
    }

    public static byte[] CreateTruncatedMovie ()
    {
        var bytes = Create(includeMediaData: false);
        return bytes[..^1];
    }

    public static byte[] CreateUndersizedExtendedBox ()
    {
        return ExtendedBoxHeader("free", 15);
    }

    public static byte[] CreateWithoutMovie ()
    {
        return CreateFileType();
    }

    private static byte[] CreateFileType ()
    {
        var payload = new byte[16];
        WriteFourCc(payload.AsSpan(0, 4), "isom");
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), 512);
        WriteFourCc(payload.AsSpan(8, 4), "isom");
        WriteFourCc(payload.AsSpan(12, 4), "mp42");
        return Box("ftyp", payload);
    }

    private static byte[] CreateMovieHeader (ulong duration)
    {
        var payload = new byte[100];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12, 4), MediaTimescale);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(16, 4), checked((uint)duration));
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(20, 4), 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(24, 2), 0x0100);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(96, 4), 2);
        return Box("mvhd", payload);
    }

    private static byte[] CreateTrack (
        uint trackId,
        string handlerType,
        string codec,
        int trackWidth,
        int trackHeight,
        int sampleEntryWidth,
        int sampleEntryHeight,
        IReadOnlyList<(uint SampleCount, uint SampleDelta)> timeToSampleEntries,
        ulong mediaDuration,
        ulong trackDuration,
        bool includeAvcConfiguration,
        bool useCompactSampleSizes,
        bool use64BitChunkOffsets,
        uint sampleCount,
        uint sampleSize,
        uint samplesPerChunk,
        ulong chunkOffset,
        bool includeEditList,
        bool includeCompositionTimeOffsets)
    {
        var sampleTableParts = new List<byte[]>
        {
            CreateSampleDescription(
                codec,
                sampleEntryWidth,
                sampleEntryHeight,
                includeAvcConfiguration),
            CreateTimeToSample(timeToSampleEntries),
            CreateSampleSize(sampleCount, sampleSize, useCompactSampleSizes),
            CreateSampleToChunk(samplesPerChunk),
            CreateChunkOffset(chunkOffset, use64BitChunkOffsets),
            Box("free", [0x99]),
        };
        if (includeCompositionTimeOffsets)
        {
            sampleTableParts.Add(Box("ctts", new byte[8]));
        }

        var trackParts = new List<byte[]>
        {
            CreateTrackHeader(trackId, trackWidth, trackHeight, trackDuration),
        };
        if (includeEditList)
        {
            trackParts.Add(Box("edts", Box("elst", new byte[8])));
        }
        trackParts.Add(
            Box(
                "mdia",
                Concatenate(
                [
                    CreateMediaHeader(mediaDuration),
                    CreateHandler(handlerType),
                    Box("minf", Box("stbl", Concatenate(sampleTableParts))),
                ])));
        return Box("trak", Concatenate(trackParts));
    }

    private static byte[] CreateTrackHeader (
        uint trackId,
        int width,
        int height,
        ulong duration)
    {
        var payload = new byte[84];
        payload[3] = 0x03;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12, 4), trackId);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(20, 4), checked((uint)duration));
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(40, 4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(56, 4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(72, 4), 0x40000000);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(76, 4), checked((uint)width << 16));
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(80, 4), checked((uint)height << 16));
        return Box("tkhd", payload);
    }

    private static byte[] CreateMediaHeader (ulong duration)
    {
        var payload = new byte[24];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12, 4), MediaTimescale);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(16, 4), checked((uint)duration));
        return Box("mdhd", payload);
    }

    private static byte[] CreateHandler (string handlerType)
    {
        var payload = new byte[24];
        WriteFourCc(payload.AsSpan(8, 4), handlerType);
        return Box("hdlr", payload);
    }

    private static byte[] CreateSampleDescription (
        string codec,
        int width,
        int height,
        bool includeAvcConfiguration)
    {
        byte[] sampleEntry;
        if (codec is "avc1" or "avc3" or "hvc1")
        {
            var visualPayload = new byte[78];
            BinaryPrimitives.WriteUInt16BigEndian(visualPayload.AsSpan(6, 2), 1);
            BinaryPrimitives.WriteUInt16BigEndian(visualPayload.AsSpan(24, 2), checked((ushort)width));
            BinaryPrimitives.WriteUInt16BigEndian(visualPayload.AsSpan(26, 2), checked((ushort)height));
            BinaryPrimitives.WriteUInt32BigEndian(visualPayload.AsSpan(28, 4), 0x00480000);
            BinaryPrimitives.WriteUInt32BigEndian(visualPayload.AsSpan(32, 4), 0x00480000);
            BinaryPrimitives.WriteUInt16BigEndian(visualPayload.AsSpan(40, 2), 1);
            BinaryPrimitives.WriteUInt16BigEndian(visualPayload.AsSpan(74, 2), 24);
            BinaryPrimitives.WriteUInt16BigEndian(visualPayload.AsSpan(76, 2), ushort.MaxValue);
            var childBoxes = includeAvcConfiguration
                ? Box(
                    "avcC",
                    [
                        1,
                        66,
                        0xC0,
                        13,
                        0xFF,
                        0xE1,
                        0,
                        25,
                        0x67, 0x42, 0xC0, 0x0D, 0x95, 0xB0, 0x50, 0x7E,
                        0xC0, 0x44, 0x00, 0x00, 0x03, 0x00, 0x04, 0x00,
                        0x00, 0x03, 0x00, 0xF0, 0x36, 0x82, 0x21, 0x1B,
                        0x80,
                        1,
                        0,
                        4,
                        0x68, 0xCA, 0x8F, 0x20,
                    ])
                : [];
            sampleEntry = Box(codec, Concatenate([visualPayload, childBoxes]));
        }
        else
        {
            sampleEntry = Box(codec, new byte[28]);
        }

        var payload = new byte[8 + sampleEntry.Length];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), 1);
        sampleEntry.CopyTo(payload, 8);
        return Box("stsd", payload);
    }

    private static byte[] CreateTimeToSample (IReadOnlyList<(uint SampleCount, uint SampleDelta)> entries)
    {
        var payload = new byte[checked(8 + (entries.Count * 8))];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), checked((uint)entries.Count));
        for (var index = 0; index < entries.Count; index++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8 + (index * 8), 4), entries[index].SampleCount);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12 + (index * 8), 4), entries[index].SampleDelta);
        }

        return Box("stts", payload);
    }

    private static byte[] CreateSampleSize (
        uint sampleCount,
        uint sampleSize,
        bool useCompactSampleSizes)
    {
        if (useCompactSampleSizes)
        {
            if (sampleSize > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleSize),
                    sampleSize,
                    "Compact synthetic sample sizes must fit one UInt16 value.");
            }

            var packedLength = checked((int)sampleCount * 2);
            var payload = new byte[checked(12 + packedLength)];
            payload[7] = 16;
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8, 4), sampleCount);
            for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(
                    payload.AsSpan(checked(12 + ((int)sampleIndex * 2)), 2),
                    checked((ushort)sampleSize));
            }
            return Box("stz2", payload);
        }

        if (sampleSize != 0)
        {
            var payload = new byte[12];
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), sampleSize);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8, 4), sampleCount);
            return Box("stsz", payload);
        }

        var variablePayload = new byte[checked(12 + ((int)sampleCount * 4))];
        BinaryPrimitives.WriteUInt32BigEndian(variablePayload.AsSpan(8, 4), sampleCount);
        return Box("stsz", variablePayload);
    }

    private static byte[] CreateSampleToChunk (uint samplesPerChunk)
    {
        var payload = new byte[20];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12, 4), samplesPerChunk);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(16, 4), 1);
        return Box("stsc", payload);
    }

    private static byte[] CreateChunkOffset (
        ulong chunkOffset,
        bool use64BitChunkOffsets)
    {
        var payload = new byte[use64BitChunkOffsets ? 16 : 12];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), 1);
        if (use64BitChunkOffsets)
        {
            BinaryPrimitives.WriteUInt64BigEndian(payload.AsSpan(8, 8), chunkOffset);
            return Box("co64", payload);
        }

        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8, 4), checked((uint)chunkOffset));
        return Box("stco", payload);
    }

    private static byte[] CreateMediaData (
        uint sampleCount,
        uint sampleSize,
        byte[]? samplePayload)
    {
        var bytes = new byte[checked((int)(sampleCount * sampleSize))];
        var effectiveSamplePayload = samplePayload
            ?? (sampleSize == EncodedSampleSize ? RecorderEncodedSample : []);
        if (effectiveSamplePayload.Length != 0 && effectiveSamplePayload.Length != sampleSize)
        {
            throw new InvalidOperationException("AVC sample payload size does not match the fixture sample-size table.");
        }
        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            var sampleOffset = checked((int)(sampleIndex * sampleSize));
            if (effectiveSamplePayload.Length != 0)
            {
                effectiveSamplePayload.CopyTo(bytes, sampleOffset);
            }
        }

        return bytes;
    }

    private static ulong CalculateDuration (IReadOnlyList<(uint SampleCount, uint SampleDelta)> entries)
    {
        ulong duration = 0;
        foreach (var entry in entries)
        {
            duration = checked(duration + ((ulong)entry.SampleCount * entry.SampleDelta));
        }

        return duration;
    }

    private static byte[] Box (
        string type,
        byte[] payload,
        bool extendedSize = false)
    {
        var headerSize = extendedSize ? 16 : 8;
        var bytes = new byte[checked(headerSize + payload.Length)];
        if (extendedSize)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), 1);
            WriteFourCc(bytes.AsSpan(4, 4), type);
            BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(8, 8), checked((ulong)bytes.Length));
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), checked((uint)bytes.Length));
            WriteFourCc(bytes.AsSpan(4, 4), type);
        }

        payload.CopyTo(bytes, headerSize);
        return bytes;
    }

    private static byte[] SizeZeroBox (
        string type,
        byte[] payload)
    {
        var bytes = new byte[checked(8 + payload.Length)];
        WriteFourCc(bytes.AsSpan(4, 4), type);
        payload.CopyTo(bytes, 8);
        return bytes;
    }

    private static byte[] ExtendedBoxHeader (
        string type,
        ulong size)
    {
        var bytes = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), 1);
        WriteFourCc(bytes.AsSpan(4, 4), type);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(8, 8), size);
        return bytes;
    }

    private static byte[] Concatenate (IEnumerable<byte[]> parts)
    {
        var result = new byte[parts.Sum(static part => part.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }

    private static void WriteFourCc (
        Span<byte> destination,
        string value)
    {
        if (value.Length != 4 || Encoding.ASCII.GetByteCount(value) != 4)
        {
            throw new ArgumentException("Box type must contain exactly four ASCII characters.", nameof(value));
        }

        _ = Encoding.ASCII.GetBytes(value, destination);
    }

    private static int FindBoxType (
        byte[] bytes,
        string type)
    {
        Span<byte> encodedType = stackalloc byte[4];
        WriteFourCc(encodedType, type);
        for (var offset = 4; offset <= bytes.Length - encodedType.Length; offset++)
        {
            if (bytes.AsSpan(offset, encodedType.Length).SequenceEqual(encodedType))
            {
                return offset;
            }
        }

        throw new InvalidOperationException($"Synthetic MP4 does not contain a {type} box.");
    }
}
