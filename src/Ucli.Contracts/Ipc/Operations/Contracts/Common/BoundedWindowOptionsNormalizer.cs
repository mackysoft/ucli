using System.Globalization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Normalizes bounded query window inputs shared by raw operations and typed queries. </summary>
public static class BoundedWindowOptionsNormalizer
{
    /// <summary> Attempts to create normalized bounded window options. </summary>
    public static bool TryNormalize (
        int? limit,
        string? cursor,
        out BoundedWindowOptions options,
        out string errorMessage)
    {
        return TryNormalize(
            all: false,
            limit,
            cursor,
            allConflictMessage: string.Empty,
            cursorErrorMessage: "cursor is invalid.",
            out options,
            out _,
            out errorMessage);
    }

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
        return TryNormalize(
            all,
            limit,
            cursor,
            allConflictMessage,
            cursorErrorMessage,
            out options,
            out _,
            out errorMessage);
    }

    /// <summary> Attempts to create normalized bounded window options with failure field reporting. </summary>
    public static bool TryNormalize (
        bool all,
        int? limit,
        string? cursor,
        string allConflictMessage,
        string cursorErrorMessage,
        out BoundedWindowOptions options,
        out BoundedWindowInvalidField invalidField,
        out string errorMessage)
    {
        options = new BoundedWindowOptions(All: false, Limit: BoundedWindowConstants.DefaultLimit, Cursor: null, Offset: 0);
        invalidField = BoundedWindowInvalidField.None;
        errorMessage = string.Empty;

        if (all && (limit.HasValue || cursor is not null))
        {
            invalidField = BoundedWindowInvalidField.All;
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
            invalidField = BoundedWindowInvalidField.Limit;
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
            invalidField = BoundedWindowInvalidField.Cursor;
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
}
