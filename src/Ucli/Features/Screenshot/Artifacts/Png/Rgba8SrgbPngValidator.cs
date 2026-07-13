using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Screenshot.Artifacts.Png;

/// <summary> Strictly validates the PNG structure and decoded pixels produced for one raw screenshot. </summary>
internal sealed class Rgba8SrgbPngValidator
{
    private const int BytesPerPixel = 4;

    /// <summary> Validates one PNG against its source raw rows without taking ownership of either stream. </summary>
    /// <param name="pngStream"> The PNG stream positioned at its signature. </param>
    /// <param name="rawStream"> The expected top-down RGBA8 sRGB source bytes. </param>
    /// <param name="expectedWidth"> The expected image width. </param>
    /// <param name="expectedHeight"> The expected image height. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes after the complete PNG and raw source are validated. </returns>
    /// <exception cref="InvalidDataException"> Thrown when either stream violates the screenshot image contract. </exception>
    public async ValueTask ValidateAsync (
        Stream pngStream,
        Stream rawStream,
        int expectedWidth,
        int expectedHeight,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pngStream);
        ArgumentNullException.ThrowIfNull(rawStream);
        if (!pngStream.CanRead)
        {
            throw new ArgumentException("PNG stream must be readable.", nameof(pngStream));
        }

        if (!rawStream.CanRead)
        {
            throw new ArgumentException("Raw screenshot stream must be readable.", nameof(rawStream));
        }

        var rowByteCount = GetRowByteCount(expectedWidth, expectedHeight);
        await ValidateSignatureAsync(pngStream, cancellationToken).ConfigureAwait(false);

        var ihdrHeader = await ReadHeaderAsync(pngStream, cancellationToken).ConfigureAwait(false);
        if (ihdrHeader.Type != PngFormat.IhdrChunkType || ihdrHeader.Length != 13)
        {
            throw new InvalidDataException("PNG must begin with one 13-byte IHDR chunk.");
        }

        var ihdr = await ReadSmallChunkAsync(pngStream, ihdrHeader, cancellationToken).ConfigureAwait(false);
        ValidateIhdr(ihdr, expectedWidth, expectedHeight);

        var srgbHeader = await ReadHeaderAsync(pngStream, cancellationToken).ConfigureAwait(false);
        if (srgbHeader.Type != PngFormat.SrgbChunkType || srgbHeader.Length != 1)
        {
            throw new InvalidDataException("PNG must contain one sRGB chunk immediately after IHDR.");
        }

        var srgb = await ReadSmallChunkAsync(pngStream, srgbHeader, cancellationToken).ConfigureAwait(false);
        if (srgb[0] != 0)
        {
            throw new InvalidDataException("Screenshot PNG must use the perceptual sRGB rendering intent.");
        }

        var firstIdatHeader = await ReadHeaderAsync(pngStream, cancellationToken).ConfigureAwait(false);
        if (firstIdatHeader.Type != PngFormat.IdatChunkType)
        {
            throw new InvalidDataException("Screenshot PNG must place IDAT immediately after sRGB metadata.");
        }

        var idatStream = new IdatChunkReadStream(pngStream, firstIdatHeader);
        await using (var decompressedStream = new ZLibStream(idatStream, CompressionMode.Decompress, leaveOpen: true))
        {
            await ValidateDecodedRowsAsync(
                    decompressedStream,
                    rawStream,
                    rowByteCount,
                    expectedHeight,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await idatStream.EnsureCompleteAsync(cancellationToken).ConfigureAwait(false);
        var iendHeader = idatStream.FollowingChunk
            ?? throw new InvalidDataException("Screenshot PNG ended without an IEND chunk.");
        if (iendHeader.Type != PngFormat.IendChunkType || iendHeader.Length != 0)
        {
            throw new InvalidDataException("Screenshot PNG may contain only IHDR, sRGB, contiguous IDAT, and an empty IEND chunk.");
        }

        _ = await ReadSmallChunkAsync(pngStream, iendHeader, cancellationToken).ConfigureAwait(false);
        if (await HasMoreDataAsync(pngStream, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidDataException("Screenshot PNG contains bytes after IEND.");
        }
    }

    private static int GetRowByteCount (
        int width,
        int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Screenshot width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Screenshot height must be positive.");
        }

        if (width > IpcScreenshotCaptureLimits.MaximumDimension
            || height > IpcScreenshotCaptureLimits.MaximumDimension)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                $"Screenshot dimensions must not exceed {IpcScreenshotCaptureLimits.MaximumDimension} pixels per axis.");
        }

        try
        {
            var sizeBytes = checked((long)width * height * BytesPerPixel);
            if (sizeBytes > IpcScreenshotCaptureLimits.MaximumRawImageBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    width,
                    $"Screenshot raw image must not exceed {IpcScreenshotCaptureLimits.MaximumRawImageBytes} bytes.");
            }

            return checked(width * BytesPerPixel);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                $"Screenshot dimensions exceed the supported raw image size. {exception.Message}");
        }
    }

    private static async ValueTask ValidateSignatureAsync (
        Stream stream,
        CancellationToken cancellationToken)
    {
        var signature = new byte[PngFormat.Signature.Length];
        await ReadExactlyAsync(stream, signature, "PNG signature is truncated.", cancellationToken).ConfigureAwait(false);
        if (!signature.AsSpan().SequenceEqual(PngFormat.Signature.Span))
        {
            throw new InvalidDataException("PNG signature is invalid.");
        }
    }

    private static void ValidateIhdr (
        ReadOnlySpan<byte> ihdr,
        int expectedWidth,
        int expectedHeight)
    {
        var width = BinaryPrimitives.ReadUInt32BigEndian(ihdr[..4]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(ihdr.Slice(4, 4));
        if (width != checked((uint)expectedWidth) || height != checked((uint)expectedHeight))
        {
            throw new InvalidDataException(
                $"PNG IHDR dimensions do not match capture metadata. Expected={expectedWidth}x{expectedHeight}, Actual={width}x{height}.");
        }

        if (ihdr[8] != 8
            || ihdr[9] != 6
            || ihdr[10] != 0
            || ihdr[11] != 0
            || ihdr[12] != 0)
        {
            throw new InvalidDataException("Screenshot PNG must be non-interlaced 8-bit RGBA with standard compression and filtering.");
        }
    }

    private static async ValueTask ValidateDecodedRowsAsync (
        Stream decompressedStream,
        Stream rawStream,
        int rowByteCount,
        int height,
        CancellationToken cancellationToken)
    {
        var decodedRow = ArrayPool<byte>.Shared.Rent(rowByteCount + 1);
        var rawRow = ArrayPool<byte>.Shared.Rent(rowByteCount);
        try
        {
            for (var row = 0; row < height; row++)
            {
                await ReadExactlyAsync(
                        decompressedStream,
                        decodedRow.AsMemory(0, rowByteCount + 1),
                        "PNG image data is truncated.",
                        cancellationToken)
                    .ConfigureAwait(false);
                if (decodedRow[0] != 0)
                {
                    throw new InvalidDataException("Screenshot PNG uses an unexpected scanline filter.");
                }

                await ReadExactlyAsync(
                        rawStream,
                        rawRow.AsMemory(0, rowByteCount),
                        "Raw screenshot is truncated.",
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!decodedRow.AsSpan(1, rowByteCount).SequenceEqual(rawRow.AsSpan(0, rowByteCount)))
                {
                    throw new InvalidDataException($"PNG decoded pixels differ from raw screenshot row {row}.");
                }
            }

            if (await HasMoreDataAsync(decompressedStream, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidDataException("PNG contains decoded pixels beyond its IHDR dimensions.");
            }

            if (await HasMoreDataAsync(rawStream, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidDataException("Raw screenshot contains bytes beyond its declared dimensions.");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(decodedRow);
            ArrayPool<byte>.Shared.Return(rawRow);
        }
    }

    private static async ValueTask<PngChunkHeader> ReadHeaderAsync (
        Stream stream,
        CancellationToken cancellationToken)
    {
        var headerBytes = new byte[8];
        await ReadExactlyAsync(stream, headerBytes, "PNG chunk header is truncated.", cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadUInt32BigEndian(headerBytes.AsSpan(0, 4));
        if (length > int.MaxValue)
        {
            throw new InvalidDataException("PNG chunk exceeds the supported size.");
        }

        return new PngChunkHeader(
            checked((int)length),
            BinaryPrimitives.ReadUInt32BigEndian(headerBytes.AsSpan(4, 4)));
    }

    private static async ValueTask<byte[]> ReadSmallChunkAsync (
        Stream stream,
        PngChunkHeader header,
        CancellationToken cancellationToken)
    {
        if (header.Length > 32)
        {
            throw new InvalidDataException("Unexpectedly large PNG metadata chunk.");
        }

        var data = new byte[header.Length];
        await ReadExactlyAsync(stream, data, "PNG chunk data is truncated.", cancellationToken).ConfigureAwait(false);
        await ValidateChunkCrcAsync(stream, header.Type, data, cancellationToken).ConfigureAwait(false);
        return data;
    }

    private static async ValueTask ValidateChunkCrcAsync (
        Stream stream,
        uint type,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        var crcBytes = new byte[4];
        await ReadExactlyAsync(stream, crcBytes, "PNG chunk CRC is truncated.", cancellationToken).ConfigureAwait(false);
        Span<byte> typeBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(typeBytes, type);
        var crc = PngCrc32.Start();
        crc = PngCrc32.Append(crc, typeBytes);
        crc = PngCrc32.Append(crc, data.Span);
        var expectedCrc = PngCrc32.Finish(crc);
        var actualCrc = BinaryPrimitives.ReadUInt32BigEndian(crcBytes);
        if (actualCrc != expectedCrc)
        {
            throw new InvalidDataException("PNG chunk CRC is invalid.");
        }
    }

    private static async ValueTask ReadExactlyAsync (
        Stream stream,
        Memory<byte> buffer,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new InvalidDataException(failureMessage);
            }

            offset += bytesRead;
        }
    }

    private static async ValueTask<bool> HasMoreDataAsync (
        Stream stream,
        CancellationToken cancellationToken)
    {
        var oneByte = new byte[1];
        return await stream.ReadAsync(oneByte, cancellationToken).ConfigureAwait(false) != 0;
    }

    private readonly record struct PngChunkHeader (
        int Length,
        uint Type);

    private sealed class IdatChunkReadStream : Stream
    {
        private readonly byte[] crcBytes = new byte[4];
        private readonly byte[] headerBytes = new byte[8];
        private readonly Stream pngStream;

        private uint crc;
        private int remainingChunkBytes;
        private bool reachedFollowingChunk;

        public IdatChunkReadStream (
            Stream pngStream,
            PngChunkHeader firstHeader)
        {
            this.pngStream = pngStream;
            BeginIdat(firstHeader);
        }

        public PngChunkHeader? FollowingChunk { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush ()
        {
        }

        public override int Read (
            byte[] buffer,
            int offset,
            int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        public override async ValueTask<int> ReadAsync (
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty || reachedFollowingChunk)
            {
                return 0;
            }

            while (remainingChunkBytes == 0)
            {
                var followingHeader = await FinishChunkAndReadFollowingHeaderAsync(cancellationToken).ConfigureAwait(false);
                if (followingHeader.Type != PngFormat.IdatChunkType)
                {
                    FollowingChunk = followingHeader;
                    reachedFollowingChunk = true;
                    return 0;
                }

                BeginIdat(followingHeader);
            }

            var requestedByteCount = Math.Min(buffer.Length, remainingChunkBytes);
            var bytesRead = await pngStream.ReadAsync(buffer[..requestedByteCount], cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new InvalidDataException("PNG IDAT data is truncated.");
            }

            crc = PngCrc32.Append(crc, buffer.Span[..bytesRead]);
            remainingChunkBytes -= bytesRead;
            return bytesRead;
        }

        public override long Seek (
            long offset,
            SeekOrigin origin)
        {
            throw new NotSupportedException();
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

        public async ValueTask EnsureCompleteAsync (CancellationToken cancellationToken)
        {
            if (reachedFollowingChunk)
            {
                return;
            }

            if (remainingChunkBytes != 0)
            {
                throw new InvalidDataException("PNG IDAT contains compressed bytes after the zlib stream ended.");
            }

            var followingHeader = await FinishChunkAndReadFollowingHeaderAsync(cancellationToken).ConfigureAwait(false);
            if (followingHeader.Type == PngFormat.IdatChunkType)
            {
                throw new InvalidDataException("PNG contains IDAT chunks after the zlib stream ended.");
            }

            FollowingChunk = followingHeader;
            reachedFollowingChunk = true;
        }

        private void BeginIdat (PngChunkHeader header)
        {
            remainingChunkBytes = header.Length;
            Span<byte> typeBytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(typeBytes, header.Type);
            crc = PngCrc32.Append(PngCrc32.Start(), typeBytes);
        }

        private async ValueTask<PngChunkHeader> FinishChunkAndReadFollowingHeaderAsync (CancellationToken cancellationToken)
        {
            await Rgba8SrgbPngValidator
                .ReadExactlyAsync(pngStream, crcBytes, "PNG IDAT CRC is truncated.", cancellationToken)
                .ConfigureAwait(false);
            var actualCrc = BinaryPrimitives.ReadUInt32BigEndian(crcBytes);
            var expectedCrc = PngCrc32.Finish(crc);
            if (actualCrc != expectedCrc)
            {
                throw new InvalidDataException("PNG IDAT CRC is invalid.");
            }

            await Rgba8SrgbPngValidator
                .ReadExactlyAsync(pngStream, headerBytes, "PNG ended after IDAT without IEND.", cancellationToken)
                .ConfigureAwait(false);
            var length = BinaryPrimitives.ReadUInt32BigEndian(headerBytes.AsSpan(0, 4));
            if (length > int.MaxValue)
            {
                throw new InvalidDataException("PNG chunk exceeds the supported size.");
            }

            return new PngChunkHeader(
                checked((int)length),
                BinaryPrimitives.ReadUInt32BigEndian(headerBytes.AsSpan(4, 4)));
        }
    }
}
