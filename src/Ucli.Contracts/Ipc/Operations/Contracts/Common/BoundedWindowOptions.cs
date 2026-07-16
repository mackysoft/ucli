namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents normalized bounded or unbounded result windowing options. </summary>
internal sealed class BoundedWindowOptions
{
    private BoundedWindowOptions (
        int? limit,
        string? cursor,
        int offset)
    {
        Limit = limit;
        Cursor = cursor;
        Offset = offset;
    }

    /// <summary> Gets the single valid unbounded option state. </summary>
    public static BoundedWindowOptions Unbounded { get; } = new(
        limit: null,
        cursor: null,
        offset: 0);

    /// <summary> Gets the normalized page limit, or <see langword="null" /> for an unbounded result. </summary>
    public int? Limit { get; }

    /// <summary> Gets the canonical starting cursor for a bounded result. </summary>
    public string? Cursor { get; }

    /// <summary> Gets the decoded starting offset. </summary>
    public int Offset { get; }

    /// <summary> Creates bounded options, applying the default limit when no explicit limit is supplied. </summary>
    /// <param name="limit"> The requested limit, or <see langword="null" /> to use the default. </param>
    /// <param name="cursor"> The canonical cursor, or <see langword="null" /> to start at offset zero. </param>
    /// <returns> Valid bounded options. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="limit" /> is outside the supported range. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="cursor" /> is not canonical. </exception>
    public static BoundedWindowOptions CreateBounded (
        int? limit,
        string? cursor)
    {
        if (TryCreateBounded(limit, cursor, out var options, out var failure))
        {
            return options;
        }

        if (failure == BoundedWindowOptionsCreationFailure.LimitOutOfRange)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                $"Limit must be between 1 and {BoundedWindowConstants.MaxLimit}.");
        }

        throw new ArgumentException("Cursor must use the canonical bounded-window representation.", nameof(cursor));
    }

    /// <summary> Attempts to create normalized options from raw query window inputs. </summary>
    /// <param name="all"> Whether to create the unbounded option state. </param>
    /// <param name="limit"> The requested limit, or <see langword="null" /> to use the default in bounded mode. </param>
    /// <param name="cursor"> The canonical cursor, or <see langword="null" /> to start at offset zero. </param>
    /// <param name="options"> The valid options when this method returns <see langword="true" />. </param>
    /// <param name="failure"> The finite failure reason when this method returns <see langword="false" />. </param>
    /// <returns> <see langword="true" /> when one valid option state was created; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        bool all,
        int? limit,
        string? cursor,
        out BoundedWindowOptions options,
        out BoundedWindowOptionsCreationFailure failure)
    {
        options = Unbounded;
        failure = BoundedWindowOptionsCreationFailure.None;

        if (all)
        {
            if (limit.HasValue || cursor != null)
            {
                failure = BoundedWindowOptionsCreationFailure.AllConflict;
                return false;
            }

            return true;
        }

        return TryCreateBounded(limit, cursor, out options, out failure);
    }

    private static bool TryCreateBounded (
        int? limit,
        string? cursor,
        out BoundedWindowOptions options,
        out BoundedWindowOptionsCreationFailure failure)
    {
        options = Unbounded;
        var normalizedLimit = limit ?? BoundedWindowConstants.DefaultLimit;
        if (normalizedLimit is < 1 or > BoundedWindowConstants.MaxLimit)
        {
            failure = BoundedWindowOptionsCreationFailure.LimitOutOfRange;
            return false;
        }

        var offset = 0;
        if (cursor != null && !BoundedWindowCursorCodec.TryDecode(cursor, out offset))
        {
            failure = BoundedWindowOptionsCreationFailure.InvalidCursor;
            return false;
        }

        options = new BoundedWindowOptions(normalizedLimit, cursor, offset);
        failure = BoundedWindowOptionsCreationFailure.None;
        return true;
    }
}
