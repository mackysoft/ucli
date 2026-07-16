using System.Buffers.Binary;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Encodes contract identifiers as canonical case-insensitive-filesystem-safe path segments. </summary>
internal static class StoragePathSegmentCodec
{
    private const int GuidByteCount = 16;
    private const string Base32HexAlphabet = "0123456789abcdefghijklmnopqrstuv";

    /// <summary> Encodes a project fingerprint as an unpadded lowercase Base32hex path segment. </summary>
    internal static string EncodeProjectFingerprint (ProjectFingerprint projectFingerprint)
    {
        if (projectFingerprint == null)
        {
            throw new ArgumentNullException(nameof(projectFingerprint));
        }

        return EncodeCanonicalHex(projectFingerprint.ToString(), Sha256LowerHex.ByteCount);
    }

    /// <summary> Encodes a SHA-256 digest as an unpadded lowercase Base32hex path segment. </summary>
    internal static string EncodeSha256Digest (Sha256Digest digest)
    {
        if (digest == null)
        {
            throw new ArgumentNullException(nameof(digest));
        }

        return EncodeCanonicalHex(digest.ToString(), Sha256LowerHex.ByteCount);
    }

    /// <summary> Returns whether a path segment is one canonical encoded SHA-256 digest. </summary>
    internal static bool IsEncodedSha256Digest (string? segment)
    {
        Span<byte> bytes = stackalloc byte[Sha256LowerHex.ByteCount];
        return TryDecodeBase32Hex(segment, bytes);
    }

    /// <summary> Encodes a non-empty GUID in canonical text byte order as an unpadded lowercase Base32hex path segment. </summary>
    internal static string EncodeGuid (
        Guid value,
        string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("GUID must not be empty.", parameterName);
        }

        Span<char> canonicalHex = stackalloc char[GuidByteCount * 2];
        value.TryFormat(canonicalHex, out _, "N");

        return EncodeCanonicalHex(canonicalHex, GuidByteCount);
    }

    /// <summary> Decodes a canonical non-empty GUID path segment. </summary>
    internal static bool TryDecodeNonEmptyGuid (
        string? segment,
        out Guid value)
    {
        Span<byte> bytes = stackalloc byte[GuidByteCount];
        if (!TryDecodeBase32Hex(segment, bytes))
        {
            value = Guid.Empty;
            return false;
        }

        value = new Guid(
            BinaryPrimitives.ReadInt32BigEndian(bytes),
            BinaryPrimitives.ReadInt16BigEndian(bytes.Slice(4)),
            BinaryPrimitives.ReadInt16BigEndian(bytes.Slice(6)),
            bytes[8],
            bytes[9],
            bytes[10],
            bytes[11],
            bytes[12],
            bytes[13],
            bytes[14],
            bytes[15]);
        return value != Guid.Empty;
    }

    private static string EncodeCanonicalHex (
        ReadOnlySpan<char> canonicalHex,
        int byteCount)
    {
        Span<byte> bytes = stackalloc byte[byteCount];
        for (var byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
        {
            var highNibble = GetCanonicalLowerHexValue(canonicalHex[byteIndex * 2]);
            var lowNibble = GetCanonicalLowerHexValue(canonicalHex[(byteIndex * 2) + 1]);
            bytes[byteIndex] = (byte)((highNibble << 4) | lowNibble);
        }

        return EncodeBase32Hex(bytes);
    }

    private static string EncodeBase32Hex (ReadOnlySpan<byte> bytes)
    {
        var encodedLength = ((bytes.Length * 8) + 4) / 5;
        Span<char> encoded = stackalloc char[encodedLength];
        uint buffer = 0;
        var bufferedBitCount = 0;
        var encodedIndex = 0;

        for (var byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
        {
            buffer = (buffer << 8) | bytes[byteIndex];
            bufferedBitCount += 8;

            while (bufferedBitCount >= 5)
            {
                bufferedBitCount -= 5;
                encoded[encodedIndex++] = Base32HexAlphabet[(int)((buffer >> bufferedBitCount) & 0x1F)];
            }

            buffer = bufferedBitCount == 0
                ? 0
                : buffer & ((1u << bufferedBitCount) - 1);
        }

        if (bufferedBitCount > 0)
        {
            encoded[encodedIndex++] = Base32HexAlphabet[(int)((buffer << (5 - bufferedBitCount)) & 0x1F)];
        }

        if (encodedIndex != encoded.Length)
        {
            throw new InvalidOperationException("Base32hex encoding produced an unexpected length.");
        }

        return new string(encoded);
    }

    private static bool TryDecodeBase32Hex (
        string? segment,
        Span<byte> bytes)
    {
        var expectedLength = ((bytes.Length * 8) + 4) / 5;
        if (segment == null || segment.Length != expectedLength)
        {
            return false;
        }

        bytes.Clear();
        uint buffer = 0;
        var bufferedBitCount = 0;
        var byteIndex = 0;

        for (var segmentIndex = 0; segmentIndex < segment.Length; segmentIndex++)
        {
            var value = GetBase32HexValue(segment[segmentIndex]);
            if (value < 0)
            {
                return false;
            }

            buffer = (buffer << 5) | (uint)value;
            bufferedBitCount += 5;
            if (bufferedBitCount >= 8)
            {
                bufferedBitCount -= 8;
                if (byteIndex >= bytes.Length)
                {
                    return false;
                }

                bytes[byteIndex++] = (byte)(buffer >> bufferedBitCount);
            }

            buffer = bufferedBitCount == 0
                ? 0
                : buffer & ((1u << bufferedBitCount) - 1);
        }

        return byteIndex == bytes.Length && buffer == 0;
    }

    private static int GetCanonicalLowerHexValue (char character)
    {
        return character <= '9'
            ? character - '0'
            : character - 'a' + 10;
    }

    private static int GetBase32HexValue (char character)
    {
        if (character is >= '0' and <= '9')
        {
            return character - '0';
        }

        if (character is >= 'a' and <= 'v')
        {
            return character - 'a' + 10;
        }

        return -1;
    }
}
