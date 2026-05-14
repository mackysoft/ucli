using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Decodes CLI query result window cursors. </summary>
internal static class QueryWindowCursorCodec
{
    /// <summary> Encodes one result offset as a base64url cursor for host-side tests and projections. </summary>
    public static string Encode (int offset)
    {
        return BoundedWindowCursorCodec.Encode(offset);
    }

    /// <summary> Attempts to decode one base64url cursor into a result offset. </summary>
    public static bool TryDecode (
        string? cursor,
        out int offset)
    {
        return BoundedWindowCursorCodec.TryDecode(cursor, out offset);
    }
}
