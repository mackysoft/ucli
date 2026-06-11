using System.Security.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Cryptography;

/// <summary> Computes lowercase hexadecimal SHA-256 digest strings. </summary>
internal static class Sha256LowerHex
{
    /// <summary> Gets the SHA-256 digest length in bytes. </summary>
    internal const int ByteCount = 32;

    /// <summary> Gets the SHA-256 digest length in hexadecimal characters. </summary>
    internal const int HexCharCount = ByteCount * 2;

    private const string HexChars = "0123456789abcdef";

    /// <summary> Computes a SHA-256 digest string from source bytes. </summary>
    /// <param name="bytes"> The source bytes. </param>
    /// <returns> The lowercase hexadecimal digest string. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="bytes" /> is <see langword="null" />. </exception>
    public static string Compute (byte[] bytes)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        return Compute(bytes.AsSpan());
    }

    /// <summary> Computes a SHA-256 digest string from source bytes. </summary>
    /// <param name="bytes"> The source bytes. </param>
    /// <returns> The lowercase hexadecimal digest string. </returns>
    public static string Compute (ReadOnlySpan<byte> bytes)
    {
        Span<byte> hashBytes = stackalloc byte[ByteCount];
        using var sha256 = SHA256.Create();
        if (!sha256.TryComputeHash(bytes, hashBytes, out var bytesWritten) || bytesWritten != ByteCount)
        {
            throw new InvalidOperationException("SHA-256 hash computation failed.");
        }

        return ToLowerHex(hashBytes);
    }

    /// <summary> Completes an incremental SHA-256 hash and returns its lowercase hexadecimal digest. </summary>
    /// <param name="hash"> The incremental SHA-256 hash. </param>
    /// <returns> The lowercase hexadecimal SHA-256 digest. </returns>
    internal static string GetHashAndReset (IncrementalHash hash)
    {
        if (hash == null)
        {
            throw new ArgumentNullException(nameof(hash));
        }

        Span<byte> hashBytes = stackalloc byte[ByteCount];
        if (!hash.TryGetHashAndReset(hashBytes, out var bytesWritten) || bytesWritten != ByteCount)
        {
            throw new InvalidOperationException("SHA-256 hash computation failed.");
        }

        return ToLowerHex(hashBytes);
    }

    /// <summary> Converts SHA-256 digest bytes to lowercase hexadecimal text. </summary>
    /// <param name="bytes"> The SHA-256 digest bytes. </param>
    /// <returns> The lowercase hexadecimal SHA-256 digest text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="bytes" /> is <see langword="null" />. </exception>
    public static string ToLowerHex (byte[] bytes)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        return ToLowerHex(bytes.AsSpan());
    }

    /// <summary> Converts SHA-256 digest bytes to lowercase hexadecimal text. </summary>
    /// <param name="bytes"> The SHA-256 digest bytes. </param>
    /// <returns> The lowercase hexadecimal SHA-256 digest text. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="bytes" /> length is not the SHA-256 digest byte count. </exception>
    public static string ToLowerHex (ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteCount)
        {
            throw new ArgumentException("Digest byte count must match SHA-256 length.", nameof(bytes));
        }

        Span<char> chars = stackalloc char[HexCharCount];
        var index = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            var value = bytes[i];
            chars[index] = HexChars[value >> 4];
            chars[index + 1] = HexChars[value & 0x0F];
            index += 2;
        }

        return new string(chars);
    }
}
