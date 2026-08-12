using System.Buffers.Binary;

namespace MackySoft.Ucli.ScreenshotFidelityOracle.Windows;

internal static class PngInspector
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    internal static Inspection Inspect (string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        return Inspect(File.ReadAllBytes(fullPath), fullPath);
    }

    internal static Inspection Inspect (ReadOnlySpan<byte> bytes, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (bytes.Length < PngSignature.Length || !bytes[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            throw new InvalidDataException($"File is not a PNG image: {source}");
        }

        int offset = PngSignature.Length;
        int chunkIndex = 0;
        int width = 0;
        int height = 0;
        int ihdrCount = 0;
        int srgbCount = 0;
        int idatCount = 0;
        bool idatSequenceEnded = false;

        while (offset < bytes.Length)
        {
            if (bytes.Length - offset < 12)
            {
                throw new InvalidDataException($"PNG chunk header is truncated: {source}");
            }

            uint unsignedLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (unsignedLength > int.MaxValue)
            {
                throw new InvalidDataException($"PNG chunk is too large: {source}");
            }

            int length = (int)unsignedLength;
            if (length > bytes.Length - offset - 12)
            {
                throw new InvalidDataException($"PNG chunk data is truncated: {source}");
            }

            ReadOnlySpan<byte> type = bytes.Slice(offset + 4, 4);
            int dataOffset = offset + 8;
            int crcOffset = dataOffset + length;
            uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(crcOffset, 4));
            uint actualCrc = CalculateCrc32(bytes.Slice(offset + 4, 4 + length));
            if (actualCrc != expectedCrc)
            {
                throw new InvalidDataException($"PNG chunk CRC is invalid: {source}");
            }

            if (chunkIndex == 0 && !type.SequenceEqual("IHDR"u8))
            {
                throw new InvalidDataException($"PNG IHDR must be the first chunk: {source}");
            }

            if (type.SequenceEqual("IHDR"u8))
            {
                ihdrCount++;
                if (ihdrCount != 1 || length != 13)
                {
                    throw new InvalidDataException($"PNG IHDR must appear exactly once with a 13-byte payload: {source}");
                }

                uint declaredWidth = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(dataOffset, 4));
                uint declaredHeight = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(dataOffset + 4, 4));
                if (declaredWidth == 0 || declaredHeight == 0)
                {
                    throw new InvalidDataException($"PNG IHDR dimensions must be positive: {source}");
                }

                if (bytes[dataOffset + 8] != 8
                    || bytes[dataOffset + 9] != 6
                    || bytes[dataOffset + 10] != 0
                    || bytes[dataOffset + 11] != 0
                    || bytes[dataOffset + 12] != 0)
                {
                    throw new InvalidDataException(
                        $"PNG IHDR must declare 8-bit RGBA, standard compression and filtering, and no interlace: {source}");
                }

                width = checked((int)declaredWidth);
                height = checked((int)declaredHeight);
            }
            else if (type.SequenceEqual("sRGB"u8))
            {
                srgbCount++;
                if (srgbCount != 1 || idatCount != 0 || length != 1 || bytes[dataOffset] > 3)
                {
                    throw new InvalidDataException($"PNG must contain exactly one valid sRGB chunk before IDAT: {source}");
                }
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (idatSequenceEnded)
                {
                    throw new InvalidDataException($"PNG IDAT chunks must be consecutive: {source}");
                }

                idatCount++;
            }
            else
            {
                if (idatCount != 0)
                {
                    idatSequenceEnded = true;
                }

                if (type.SequenceEqual("IEND"u8))
                {
                    if (length != 0)
                    {
                        throw new InvalidDataException($"PNG IEND must have an empty payload: {source}");
                    }

                    int endOffset = crcOffset + 4;
                    if (endOffset != bytes.Length)
                    {
                        throw new InvalidDataException($"PNG has data after IEND: {source}");
                    }

                    if (ihdrCount != 1 || srgbCount != 1 || idatCount == 0)
                    {
                        throw new InvalidDataException($"PNG is missing required IHDR, sRGB, or IDAT chunks: {source}");
                    }

                    return new Inspection(width, height);
                }
            }

            offset = crcOffset + 4;
            chunkIndex++;
        }

        throw new InvalidDataException($"PNG is missing a terminal IEND chunk: {source}");
    }

    private static uint CalculateCrc32 (ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xEDB88320u;
            }
        }

        return ~crc;
    }

    internal sealed record Inspection (int Width, int Height);
}
