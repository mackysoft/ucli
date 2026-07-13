using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Encodes and decodes unpadded base64url text values. </summary>
internal static class Base64UrlCodec
{
    /// <summary> Encodes bytes as unpadded base64url text. </summary>
    /// <param name="bytes"> The source bytes. </param>
    /// <returns> The base64url text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="bytes" /> is <see langword="null" />. </exception>
    public static string Encode (byte[] bytes)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        return Encode(bytes.AsSpan());
    }

    /// <summary> Encodes bytes as unpadded base64url text. </summary>
    /// <param name="bytes"> The source bytes. </param>
    /// <returns> The base64url text. </returns>
    public static string Encode (ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes.ToArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary> Attempts to decode canonical unpadded base64url text into bytes. </summary>
    /// <param name="text"> The base64url text without padding or outer whitespace. </param>
    /// <param name="bytes"> The decoded bytes. </param>
    /// <returns> <see langword="true" /> when decode succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryDecode (
        string? text,
        out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (!IsCanonical(text))
        {
            return false;
        }

        var remainder = text.Length % 4;
        var paddingLength = remainder == 0 ? 0 : 4 - remainder;
        var base64 = new char[text.Length + paddingLength];
        for (var index = 0; index < text.Length; index++)
        {
            base64[index] = text[index] switch
            {
                '-' => '+',
                '_' => '/',
                _ => text[index],
            };
        }

        for (var index = text.Length; index < base64.Length; index++)
        {
            base64[index] = '=';
        }

        try
        {
            bytes = Convert.FromBase64CharArray(base64, 0, base64.Length);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary> Determines whether text is canonical unpadded base64url without allocating a decode buffer. </summary>
    /// <param name="text"> The candidate base64url text. </param>
    /// <returns> <see langword="true" /> when the text uses the base64url alphabet and has zero unused trailing bits; otherwise <see langword="false" />. </returns>
    public static bool IsCanonical ([NotNullWhen(true)] string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var remainder = text.Length % 4;
        if (remainder == 1)
        {
            return false;
        }

        var lastValue = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var value = GetBase64UrlValue(text[index]);
            if (value < 0)
            {
                return false;
            }

            lastValue = value;
        }

        return (remainder != 2 || (lastValue & 0x0F) == 0)
            && (remainder != 3 || (lastValue & 0x03) == 0);
    }

    private static int GetBase64UrlValue (char character)
    {
        if (character >= 'A' && character <= 'Z')
        {
            return character - 'A';
        }

        if (character >= 'a' && character <= 'z')
        {
            return character - 'a' + 26;
        }

        if (character >= '0' && character <= '9')
        {
            return character - '0' + 52;
        }

        return character switch
        {
            '-' => 62,
            '_' => 63,
            _ => -1,
        };
    }
}
