using System.Buffers.Binary;

namespace MackySoft.Ucli.ScreenshotFidelityOracle.Windows;

internal static class PngInspector
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    internal static Inspection Inspect (string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        byte[] bytes = File.ReadAllBytes(fullPath);
        if (bytes.Length < PngSignature.Length || !bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
        {
            throw new InvalidDataException($"File is not a PNG image: {fullPath}");
        }

        bool hasSrgbChunk = false;
        int offset = PngSignature.Length;
        while (offset < bytes.Length)
        {
            if (bytes.Length - offset < 12)
            {
                throw new InvalidDataException($"PNG chunk header is truncated: {fullPath}");
            }

            uint unsignedLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            if (unsignedLength > int.MaxValue)
            {
                throw new InvalidDataException($"PNG chunk is too large: {fullPath}");
            }

            int length = (int)unsignedLength;
            int chunkEnd = checked(offset + 12 + length);
            if (chunkEnd > bytes.Length)
            {
                throw new InvalidDataException($"PNG chunk data is truncated: {fullPath}");
            }

            ReadOnlySpan<byte> type = bytes.AsSpan(offset + 4, 4);
            if (type.SequenceEqual("sRGB"u8))
            {
                if (length != 1 || bytes[offset + 8] > 3)
                {
                    throw new InvalidDataException($"PNG sRGB chunk is invalid: {fullPath}");
                }

                hasSrgbChunk = true;
            }

            if (type.SequenceEqual("IDAT"u8))
            {
                break;
            }

            offset = chunkEnd;
        }

        return new Inspection(hasSrgbChunk);
    }

    internal sealed record Inspection (bool HasSrgbChunk);
}
