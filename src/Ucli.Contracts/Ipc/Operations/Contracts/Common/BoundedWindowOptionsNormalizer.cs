using System.Globalization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Normalizes bounded query window inputs shared by raw operations and typed queries. </summary>
internal static class BoundedWindowOptionsNormalizer
{
    /// <summary> Attempts to create normalized bounded window options with optional unbounded mode support. </summary>
    public static bool TryNormalize (
        bool all,
        int? limit,
        string? cursor,
        string allConflictMessage,
        string cursorErrorMessage,
        out BoundedWindowOptions options,
        out string errorMessage)
    {
        options = new BoundedWindowOptions(All: false, Limit: BoundedWindowConstants.DefaultLimit, Cursor: null, Offset: 0);
        errorMessage = string.Empty;

        if (all && (limit.HasValue || cursor is not null))
        {
            errorMessage = allConflictMessage;
            return false;
        }

        if (all)
        {
            options = new BoundedWindowOptions(
                All: true,
                Limit: 0,
                Cursor: null,
                Offset: 0);
            return true;
        }

        var normalizedLimit = limit ?? BoundedWindowConstants.DefaultLimit;
        if (normalizedLimit < 1 || normalizedLimit > BoundedWindowConstants.MaxLimit)
        {
            errorMessage = string.Format(
                CultureInfo.InvariantCulture,
                "limit must be between 1 and {0}. Actual: {1}.",
                BoundedWindowConstants.MaxLimit,
                normalizedLimit);
            return false;
        }

        var offset = 0;
        if (cursor is not null
            && !BoundedWindowCursorCodec.TryDecode(cursor, out offset))
        {
            errorMessage = cursorErrorMessage;
            return false;
        }

        options = new BoundedWindowOptions(
            All: false,
            Limit: normalizedLimit,
            Cursor: cursor,
            Offset: offset);
        return true;
    }

    /// <summary> Creates normalized bounded window options from values already validated by operation contracts. </summary>
    public static BoundedWindowOptions NormalizeValidated (
        int? limit,
        string? cursor)
    {
        if (!TryNormalize(
            all: false,
            limit,
            cursor,
            allConflictMessage: string.Empty,
            cursorErrorMessage: "cursor is invalid.",
            out var options,
            out var errorMessage))
        {
            throw new ArgumentException(errorMessage, cursor is null ? nameof(limit) : nameof(cursor));
        }

        return options;
    }
}
