using System.Buffers.Binary;
using System.Text;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Screenshot.Artifacts.Png;

namespace MackySoft.Ucli.Tests.Features.Screenshot.Artifacts.Png;

public sealed class Rgba8SrgbPngCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task EncodeAsync_ThenValidateAsync_PreservesRgbaRowsAndWritesSrgbMetadata ()
    {
        var rawBytes = CreateTwoByTwoRawBytes();
        var pngBytes = await EncodeAsync(rawBytes, width: 2, height: 2);

        await new Rgba8SrgbPngValidator().ValidateAsync(
            new MemoryStream(pngBytes, writable: false),
            new MemoryStream(rawBytes, writable: false),
            expectedWidth: 2,
            expectedHeight: 2,
            CancellationToken.None);

        var chunks = ReadChunks(pngBytes);
        Assert.Equal(["IHDR", "sRGB", "IDAT", "IEND"], chunks.Select(static chunk => chunk.Type).ToArray());
        Assert.Equal(new byte[] { 0 }, chunks[1].Data);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(chunks[0].Data.AsSpan(0, 4)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(chunks[0].Data.AsSpan(4, 4)));
        Assert.Equal(8, chunks[0].Data[8]);
        Assert.Equal(6, chunks[0].Data[9]);
        Assert.Equal(0, chunks[0].Data[12]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EncodeAsync_WhenCompressedDataSpansIdatChunks_RemainsStrictlyDecodable ()
    {
        const int width = 256;
        const int height = 128;
        var rawBytes = new byte[width * height * 4];
        new Random(123456).NextBytes(rawBytes);

        var pngBytes = await EncodeAsync(rawBytes, width, height);

        Assert.True(ReadChunks(pngBytes).Count(static chunk => chunk.Type == "IDAT") > 1);
        await new Rgba8SrgbPngValidator().ValidateAsync(
            new MemoryStream(pngBytes, writable: false),
            new MemoryStream(rawBytes, writable: false),
            width,
            height,
            CancellationToken.None);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("crc")]
    [InlineData("truncated")]
    [InlineData("dimensions")]
    [InlineData("pixels")]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_WhenPngOrExpectedPixelsAreInconsistent_ThrowsInvalidDataException (string caseName)
    {
        var rawBytes = CreateTwoByTwoRawBytes();
        var expectedRawBytes = rawBytes.ToArray();
        var pngBytes = await EncodeAsync(rawBytes, width: 2, height: 2);
        var expectedWidth = 2;

        switch (caseName)
        {
            case "signature":
                pngBytes[0] ^= 0x01;
                break;
            case "crc":
                pngBytes[32] ^= 0x01;
                break;
            case "truncated":
                Array.Resize(ref pngBytes, pngBytes.Length - 1);
                break;
            case "dimensions":
                expectedWidth = 3;
                break;
            case "pixels":
                expectedRawBytes[0] ^= 0x01;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown corruption case.");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new Rgba8SrgbPngValidator()
                .ValidateAsync(
                    new MemoryStream(pngBytes, writable: false),
                    new MemoryStream(expectedRawBytes, writable: false),
                    expectedWidth,
                    expectedHeight: 2,
                    CancellationToken.None)
                .AsTask());
    }

    [Theory]
    [InlineData(15)]
    [InlineData(17)]
    [Trait("Size", "Small")]
    public async Task EncodeAsync_WhenRawLengthDoesNotMatchDimensions_ThrowsInvalidDataException (int rawByteCount)
    {
        var rawBytes = new byte[rawByteCount];

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new Rgba8SrgbPngEncoder()
                .EncodeAsync(
                    new MemoryStream(rawBytes, writable: false),
                    width: 2,
                    height: 2,
                    new MemoryStream(),
                    CancellationToken.None)
                .AsTask());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EncodeAsync_WhenDimensionsExceedHostLimit_RejectsBeforeReadingRawStream ()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new Rgba8SrgbPngEncoder()
                .EncodeAsync(
                    new MemoryStream(),
                    IpcScreenshotCaptureLimits.MaximumDimension + 1,
                    height: 1,
                    new MemoryStream(),
                    CancellationToken.None)
                .AsTask());
    }

    private static async ValueTask<byte[]> EncodeAsync (
        byte[] rawBytes,
        int width,
        int height)
    {
        var output = new MemoryStream();
        await new Rgba8SrgbPngEncoder().EncodeAsync(
            new MemoryStream(rawBytes, writable: false),
            width,
            height,
            output,
            CancellationToken.None);
        return output.ToArray();
    }

    private static byte[] CreateTwoByTwoRawBytes ()
    {
        return
        [
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
            255, 255, 255, 255,
        ];
    }

    private static IReadOnlyList<PngChunk> ReadChunks (byte[] pngBytes)
    {
        var chunks = new List<PngChunk>();
        var offset = 8;
        while (offset < pngBytes.Length)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(pngBytes.AsSpan(offset, 4)));
            var type = Encoding.ASCII.GetString(pngBytes, offset + 4, 4);
            var data = pngBytes.AsSpan(offset + 8, length).ToArray();
            chunks.Add(new PngChunk(type, data));
            offset = checked(offset + 12 + length);
        }

        Assert.Equal(pngBytes.Length, offset);
        return chunks;
    }

    private sealed record PngChunk (
        string Type,
        byte[] Data);
}
