using System.Globalization;
using System.Text;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query.Projection;

/// <summary> Encodes and decodes typed-query window cursors. </summary>
internal static class QueryWindowCursorCodec
{
    /// <summary> Encodes one result offset as a base64url cursor. </summary>
    public static string Encode (int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        var bytes = Encoding.UTF8.GetBytes(offset.ToString(CultureInfo.InvariantCulture));
        return Base64UrlCodec.Encode(bytes);
    }

    /// <summary> Attempts to decode one base64url cursor into a result offset. </summary>
    public static bool TryDecode (
        string? cursor,
        out int offset)
    {
        offset = 0;
        if (!Base64UrlCodec.TryDecode(cursor, out var bytes))
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(bytes);
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out offset)
            && offset >= 0;
    }
}
