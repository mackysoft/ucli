using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Bounded result window metadata.")]
public sealed record BoundedWindow
{
    [JsonConstructor]
    public BoundedWindow (
        int? limit,
        string? cursor,
        string? nextCursor,
        bool isComplete,
        int? totalCount)
    {
        Limit = limit;
        Cursor = cursor;
        NextCursor = nextCursor;
        IsComplete = isComplete;
        TotalCount = totalCount;
    }

    [UcliRequired]
    [UcliDescription("Maximum number of items requested for this response window, or null when unbounded.")]
    public int? Limit { get; init; }

    [UcliRequired]
    [UcliJsonAllowNull]
    [UcliDescription("Opaque cursor used to start this response window, or null for the first window.")]
    public string? Cursor { get; init; }

    [UcliRequired]
    [UcliJsonAllowNull]
    [UcliDescription("Opaque cursor for the next response window, or null when the result is complete.")]
    public string? NextCursor { get; init; }

    [UcliRequired]
    [UcliDescription("Whether this response window reached the end of the result set.")]
    public bool IsComplete { get; init; }

    [UcliRequired]
    [UcliDescription("Total number of items in the result set when known.")]
    public int? TotalCount { get; init; }
}
