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

    /// <summary> Attempts to decode one unpadded base64url text into bytes. </summary>
    /// <param name="text"> The base64url text. </param>
    /// <param name="bytes"> The decoded bytes. </param>
    /// <returns> <see langword="true" /> when decode succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryDecode (
        string? text,
        out byte[] bytes)
    {
        bytes = Array.Empty<byte>();

        var normalized = StringValueNormalizer.TrimToNull(text);
        if (normalized is null)
        {
            return false;
        }

        var base64 = normalized
            .Replace('-', '+')
            .Replace('_', '/');

        var padding = base64.Length % 4;
        if (padding == 2)
        {
            base64 += "==";
        }
        else if (padding == 3)
        {
            base64 += "=";
        }
        else if (padding != 0)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}