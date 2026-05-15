using System.Globalization;
using System.Text;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Encodes and decodes bounded query window cursors. </summary>
internal static class BoundedWindowCursorCodec
{
    private const int MaxEncodedOffsetCursorLength = 14;

    /// <summary> Encodes one result offset as a base64url cursor. </summary>
    public static string Encode (int offset)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must not be negative.");
        }

        var bytes = Encoding.UTF8.GetBytes(offset.ToString(CultureInfo.InvariantCulture));
        return Base64UrlCodec.Encode(bytes);
    }

    /// <summary> Attempts to decode one base64url cursor into a result offset. </summary>
    public static bool TryDecode (
        string? cursor,
        out int offset)
    {
        offset = 0;
        if (string.IsNullOrEmpty(cursor)
            || cursor.Length > MaxEncodedOffsetCursorLength
            || StringValueValidator.HasOuterWhitespace(cursor))
        {
            return false;
        }

        if (!Base64UrlCodec.TryDecode(cursor, out var bytes))
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(bytes);
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out offset)
            && offset >= 0
            && string.Equals(Encode(offset), cursor, StringComparison.Ordinal);
    }
}
