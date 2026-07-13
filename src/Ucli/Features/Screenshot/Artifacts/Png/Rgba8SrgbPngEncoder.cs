using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Screenshot.Artifacts.Png;

/// <summary> Encodes top-down, 8-bit sRGB RGBA rows into a PNG stream with explicit sRGB metadata. </summary>
internal sealed class Rgba8SrgbPngEncoder
{
    private const int BytesPerPixel = 4;
    private const int IdatBufferSize = 64 * 1024;

    /// <summary> Encodes one raw image without taking ownership of either stream. </summary>
    /// <param name="rawStream"> The exact top-down RGBA8 sRGB pixel bytes. </param>
    /// <param name="width"> The image width in pixels. </param>
    /// <param name="height"> The image height in pixels. </param>
    /// <param name="outputStream"> The destination PNG stream. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes after the PNG is fully written. </returns>
    /// <exception cref="ArgumentException"> Thrown when a stream cannot perform its required operation or dimensions are invalid. </exception>
    /// <exception cref="InvalidDataException"> Thrown when the raw stream length does not match the declared dimensions. </exception>
    public async ValueTask EncodeAsync (
        Stream rawStream,
        int width,
        int height,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawStream);
        ArgumentNullException.ThrowIfNull(outputStream);
        if (!rawStream.CanRead)
        {
            throw new ArgumentException("Raw screenshot stream must be readable.", nameof(rawStream));
        }

        if (!outputStream.CanWrite)
        {
            throw new ArgumentException("PNG output stream must be writable.", nameof(outputStream));
        }

        var rowByteCount = GetRowByteCount(width, height);
        cancellationToken.ThrowIfCancellationRequested();

        var chunkWriter = new PngChunkWriter(outputStream);
        await outputStream.WriteAsync(PngFormat.Signature, cancellationToken).ConfigureAwait(false);

        var ihdrData = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdrData.AsSpan(0, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(ihdrData.AsSpan(4, 4), checked((uint)height));
        ihdrData[8] = 8;
        ihdrData[9] = 6;
        ihdrData[10] = 0;
        ihdrData[11] = 0;
        ihdrData[12] = 0;
        await chunkWriter.WriteChunkAsync(PngFormat.IhdrChunkType, ihdrData, cancellationToken).ConfigureAwait(false);
        await chunkWriter.WriteChunkAsync(PngFormat.SrgbChunkType, new byte[] { 0 }, cancellationToken).ConfigureAwait(false);

        var rowBuffer = ArrayPool<byte>.Shared.Rent(rowByteCount);
        try
        {
            var idatStream = new IdatChunkWriteStream(chunkWriter, IdatBufferSize);
            await using (var compressedStream = new ZLibStream(
                idatStream,
                CompressionLevel.Optimal,
                leaveOpen: true))
            {
                var filterByte = new byte[] { 0 };
                for (var row = 0; row < height; row++)
                {
                    await ReadExactlyAsync(
                            rawStream,
                            rowBuffer.AsMemory(0, rowByteCount),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await compressedStream.WriteAsync(filterByte, cancellationToken).ConfigureAwait(false);
                    await compressedStream
                        .WriteAsync(rowBuffer.AsMemory(0, rowByteCount), cancellationToken)
                        .ConfigureAwait(false);
                }

                if (await HasMoreDataAsync(rawStream, cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidDataException("Raw screenshot contains bytes beyond its declared dimensions.");
                }
            }

            await idatStream.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rowBuffer);
        }

        await chunkWriter.WriteChunkAsync(PngFormat.IendChunkType, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
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

    private static async ValueTask ReadExactlyAsync (
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new InvalidDataException("Raw screenshot ended before its declared dimensions were filled.");
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

    private sealed class PngChunkWriter
    {
        private readonly byte[] header = new byte[8];
        private readonly byte[] crcBytes = new byte[4];
        private readonly Stream outputStream;

        public PngChunkWriter (Stream outputStream)
        {
            this.outputStream = outputStream;
        }

        public void WriteChunk (
            uint chunkType,
            ReadOnlySpan<byte> data)
        {
            PrepareHeaderAndCrc(chunkType, data);
            outputStream.Write(header);
            outputStream.Write(data);
            outputStream.Write(crcBytes);
        }

        public async ValueTask WriteChunkAsync (
            uint chunkType,
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken)
        {
            PrepareHeaderAndCrc(chunkType, data.Span);
            await outputStream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(crcBytes, cancellationToken).ConfigureAwait(false);
        }

        private void PrepareHeaderAndCrc (
            uint chunkType,
            ReadOnlySpan<byte> data)
        {
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), checked((uint)data.Length));
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), chunkType);
            var crc = PngCrc32.Start();
            crc = PngCrc32.Append(crc, header.AsSpan(4, 4));
            crc = PngCrc32.Append(crc, data);
            BinaryPrimitives.WriteUInt32BigEndian(crcBytes, PngCrc32.Finish(crc));
        }
    }

    private sealed class IdatChunkWriteStream : Stream
    {
        private readonly byte[] buffer;
        private readonly PngChunkWriter chunkWriter;

        private int bufferedByteCount;
        private bool completed;

        public IdatChunkWriteStream (
            PngChunkWriter chunkWriter,
            int bufferSize)
        {
            this.chunkWriter = chunkWriter;
            buffer = new byte[bufferSize];
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !completed;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush ()
        {
            FlushBufferedChunk();
        }

        public override Task FlushAsync (CancellationToken cancellationToken)
        {
            return FlushBufferedChunkAsync(cancellationToken).AsTask();
        }

        public override int Read (
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
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
            byte[] source,
            int offset,
            int count)
        {
            ArgumentNullException.ThrowIfNull(source);
            Write(source.AsSpan(offset, count));
        }

        public override void Write (ReadOnlySpan<byte> source)
        {
            ThrowIfCompleted();
            while (!source.IsEmpty)
            {
                var copyByteCount = Math.Min(source.Length, buffer.Length - bufferedByteCount);
                source[..copyByteCount].CopyTo(buffer.AsSpan(bufferedByteCount));
                bufferedByteCount += copyByteCount;
                source = source[copyByteCount..];
                if (bufferedByteCount == buffer.Length)
                {
                    FlushBufferedChunk();
                }
            }
        }

        public override async ValueTask WriteAsync (
            ReadOnlyMemory<byte> source,
            CancellationToken cancellationToken = default)
        {
            ThrowIfCompleted();
            while (!source.IsEmpty)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var copyByteCount = Math.Min(source.Length, buffer.Length - bufferedByteCount);
                source[..copyByteCount].CopyTo(buffer.AsMemory(bufferedByteCount));
                bufferedByteCount += copyByteCount;
                source = source[copyByteCount..];
                if (bufferedByteCount == buffer.Length)
                {
                    await FlushBufferedChunkAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public override Task WriteAsync (
            byte[] source,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(source);
            return WriteAsync(source.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public async ValueTask CompleteAsync (CancellationToken cancellationToken)
        {
            if (completed)
            {
                return;
            }

            await FlushBufferedChunkAsync(cancellationToken).ConfigureAwait(false);
            completed = true;
        }

        private void FlushBufferedChunk ()
        {
            if (bufferedByteCount == 0)
            {
                return;
            }

            chunkWriter.WriteChunk(PngFormat.IdatChunkType, buffer.AsSpan(0, bufferedByteCount));
            bufferedByteCount = 0;
        }

        private async ValueTask FlushBufferedChunkAsync (CancellationToken cancellationToken)
        {
            if (bufferedByteCount == 0)
            {
                return;
            }

            await chunkWriter
                .WriteChunkAsync(PngFormat.IdatChunkType, buffer.AsMemory(0, bufferedByteCount), cancellationToken)
                .ConfigureAwait(false);
            bufferedByteCount = 0;
        }

        private void ThrowIfCompleted ()
        {
            if (completed)
            {
                throw new InvalidOperationException("PNG IDAT stream has already been completed.");
            }
        }
    }

}
