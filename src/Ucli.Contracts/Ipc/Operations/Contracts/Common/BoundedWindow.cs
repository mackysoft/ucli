using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Bounded result window metadata.")]
public sealed record BoundedWindow
{
    /// <summary> Initializes internally consistent bounded result metadata. </summary>
    /// <param name="limit"> The supported page limit, or <see langword="null" /> for an unbounded result. </param>
    /// <param name="cursor"> The canonical cursor used to start the page, or <see langword="null" /> for the first page. </param>
    /// <param name="nextCursor"> The canonical cursor for an incomplete page; otherwise <see langword="null" />. </param>
    /// <param name="isComplete"> Whether the result has no subsequent page. </param>
    /// <param name="totalCount"> The non-negative total item count when known. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="limit" /> is outside the supported range or <paramref name="totalCount" /> is negative. </exception>
    /// <exception cref="ArgumentException"> Thrown when a cursor is invalid or the supplied values do not describe one consistent window. </exception>
    [JsonConstructor]
    public BoundedWindow (
        int? limit,
        string? cursor,
        string? nextCursor,
        bool isComplete,
        int? totalCount)
    {
        if (totalCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), totalCount, "Total count must not be negative.");
        }

        if (limit is int boundedLimit)
        {
            ValidateBounded(boundedLimit, cursor, nextCursor, isComplete, totalCount);
        }
        else
        {
            ValidateUnbounded(cursor, nextCursor, isComplete);
        }

        Limit = limit;
        Cursor = cursor;
        NextCursor = nextCursor;
        IsComplete = isComplete;
        TotalCount = totalCount;
    }

    private static void ValidateUnbounded (
        string? cursor,
        string? nextCursor,
        bool isComplete)
    {
        if (cursor != null)
        {
            throw new ArgumentException("An unbounded window must not specify a cursor.", nameof(cursor));
        }

        if (nextCursor != null)
        {
            throw new ArgumentException("An unbounded window must not specify a next cursor.", nameof(nextCursor));
        }

        if (!isComplete)
        {
            throw new ArgumentException("An unbounded window must be complete.", nameof(isComplete));
        }
    }

    private static void ValidateBounded (
        int limit,
        string? cursor,
        string? nextCursor,
        bool isComplete,
        int? totalCount)
    {
        if (limit is < 1 or > BoundedWindowConstants.MaxLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                $"Limit must be between 1 and {BoundedWindowConstants.MaxLimit}.");
        }

        var cursorOffset = 0;
        if (cursor != null && !BoundedWindowCursorCodec.TryDecode(cursor, out cursorOffset))
        {
            throw new ArgumentException("Cursor must use the canonical bounded-window representation.", nameof(cursor));
        }

        var nextCursorOffset = 0;
        if (nextCursor != null && !BoundedWindowCursorCodec.TryDecode(nextCursor, out nextCursorOffset))
        {
            throw new ArgumentException("Next cursor must use the canonical bounded-window representation.", nameof(nextCursor));
        }

        if (isComplete != (nextCursor == null))
        {
            throw new ArgumentException("A complete window must not have a next cursor, and an incomplete window must have one.", nameof(isComplete));
        }

        if (nextCursor == null)
        {
            return;
        }

        if (nextCursorOffset <= cursorOffset)
        {
            throw new ArgumentException("Next cursor must advance beyond the current cursor.", nameof(nextCursor));
        }

        if (nextCursorOffset - cursorOffset > limit)
        {
            throw new ArgumentException("Next cursor must not advance beyond the page limit.", nameof(nextCursor));
        }

        if (totalCount.HasValue && nextCursorOffset >= totalCount.Value)
        {
            throw new ArgumentException("An incomplete window next cursor must precede the known total count.", nameof(nextCursor));
        }
    }

    [UcliRequired]
    [UcliDescription("Maximum number of items requested for this response window, or null when unbounded.")]
    public int? Limit { get; }

    [UcliRequired]
    [UcliJsonAllowNull]
    [UcliDescription("Opaque cursor used to start this response window, or null for the first window.")]
    public string? Cursor { get; }

    [UcliRequired]
    [UcliJsonAllowNull]
    [UcliDescription("Opaque cursor for the next response window, or null when the result is complete.")]
    public string? NextCursor { get; }

    [UcliRequired]
    [UcliDescription("Whether this response window reached the end of the result set.")]
    public bool IsComplete { get; }

    [UcliRequired]
    [UcliDescription("Total number of items in the result set when known.")]
    public int? TotalCount { get; }
}
