namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Applies normalized bounded window options to flat result lists. </summary>
internal static class BoundedWindowApplicator
{
    /// <summary> Applies one window to an item list. </summary>
    public static BoundedWindowResult<T> Apply<T> (
        IReadOnlyList<T> items,
        BoundedWindowOptions options)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.Limit is not int limit)
        {
            return ApplyAll(items);
        }

        return ApplyPage(items, options, limit);
    }

    private static BoundedWindowResult<T> ApplyAll<T> (IReadOnlyList<T> items)
    {
        return new BoundedWindowResult<T>(
            Items: CopyRange(items, start: 0, count: items.Count),
            Window: new BoundedWindow(
                limit: null,
                cursor: null,
                nextCursor: null,
                isComplete: true,
                totalCount: items.Count));
    }

    private static BoundedWindowResult<T> ApplyPage<T> (
        IReadOnlyList<T> items,
        BoundedWindowOptions options,
        int limit)
    {
        var offset = Math.Min(options.Offset, items.Count);
        var remaining = items.Count - offset;
        var count = Math.Min(limit, remaining);
        var nextOffset = offset + count;
        var isComplete = nextOffset >= items.Count;
        return new BoundedWindowResult<T>(
            Items: CopyRange(items, offset, count),
            Window: new BoundedWindow(
                limit,
                cursor: options.Cursor,
                nextCursor: isComplete ? null : BoundedWindowCursorCodec.Encode(nextOffset),
                isComplete: isComplete,
                totalCount: items.Count));
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
