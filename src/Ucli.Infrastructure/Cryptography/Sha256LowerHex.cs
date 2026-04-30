using System.Security.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Cryptography;

/// <summary> Computes lowercase hexadecimal SHA-256 digest strings. </summary>
internal static class Sha256LowerHex
{
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
        var inputBytes = bytes.ToArray();
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(inputBytes);
        return ToLowerHex(hashBytes);
    }

    /// <summary> Converts bytes to lowercase hexadecimal text. </summary>
    /// <param name="bytes"> The source bytes. </param>
    /// <returns> The lowercase hexadecimal text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="bytes" /> is <see langword="null" />. </exception>
    public static string ToLowerHex (byte[] bytes)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        return ToLowerHex(bytes.AsSpan());
    }

    /// <summary> Converts bytes to lowercase hexadecimal text. </summary>
    /// <param name="bytes"> The source bytes. </param>
    /// <returns> The lowercase hexadecimal text. </returns>
    public static string ToLowerHex (ReadOnlySpan<byte> bytes)
    {
        const string HexChars = "0123456789abcdef";

        var chars = new char[bytes.Length * 2];
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
