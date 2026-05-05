namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query.Projection;

/// <summary> Applies normalized window options to list-shaped query results. </summary>
internal static class QueryWindowApplicator
{
    /// <summary> Applies one window to an item list. </summary>
    public static QueryWindowResult<T> Apply<T> (
        IReadOnlyList<T> items,
        QueryWindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(options);

        if (options.All)
        {
            return new QueryWindowResult<T>(
                Items: CopyRange(items, start: 0, count: items.Count),
                Window: new QueryWindowInfo(
                    Limit: null,
                    After: null,
                    NextCursor: null,
                    IsComplete: true,
                    TotalCount: items.Count));
        }

        var offset = Math.Min(options.Offset, items.Count);
        var remaining = items.Count - offset;
        var count = Math.Min(options.Limit, remaining);
        var nextOffset = offset + count;
        var isComplete = nextOffset >= items.Count;
        return new QueryWindowResult<T>(
            Items: CopyRange(items, offset, count),
            Window: new QueryWindowInfo(
                Limit: options.Limit,
                After: options.After,
                NextCursor: isComplete ? null : QueryWindowCursorCodec.Encode(nextOffset),
                IsComplete: isComplete,
                TotalCount: items.Count));
    }

    private static IReadOnlyList<T> CopyRange<T> (
        IReadOnlyList<T> items,
        int start,
        int count)
    {
        if (count == 0)
        {
            return [];
        }

        var result = new T[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = items[start + i];
        }

        return result;
    }
}
