using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the timestamp window used for a build log artifact. </summary>
public sealed record IpcBuildLogWindow
{
    /// <summary> Initializes one timestamp window used for a build log artifact. </summary>
    /// <param name="StartedAtUtc"> The non-default UTC timestamp captured immediately before runner invocation. </param>
    /// <param name="CompletedAtUtc"> The non-default UTC timestamp captured immediately after terminal result observation; it must not precede <paramref name="StartedAtUtc" />. </param>
    /// <param name="CursorStart"> The cursor captured before runner invocation, or <see langword="null" /> when unavailable. </param>
    /// <param name="CursorEnd"> The cursor captured after terminal result observation, or <see langword="null" /> when unavailable. </param>
    /// <exception cref="ArgumentException">
    /// Thrown when a timestamp is the default value or does not use the UTC offset, or when both cursors are present but belong to different streams.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="CompletedAtUtc" /> precedes <paramref name="StartedAtUtc" /> or <paramref name="CursorEnd" /> precedes <paramref name="CursorStart" />.
    /// </exception>
    [JsonConstructor]
    public IpcBuildLogWindow (
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc,
        IpcLogCursor? CursorStart,
        IpcLogCursor? CursorEnd)
    {
        var validatedStartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            StartedAtUtc,
            nameof(StartedAtUtc));
        var validatedCompletedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            CompletedAtUtc,
            nameof(CompletedAtUtc));
        if (validatedCompletedAtUtc < validatedStartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CompletedAtUtc),
                CompletedAtUtc,
                "Build log completion timestamp must not precede the start timestamp.");
        }

        if (CursorStart != null && CursorEnd != null)
        {
            if (CursorStart.StreamId != CursorEnd.StreamId)
            {
                throw new ArgumentException(
                    "Build log cursors must belong to the same stream.",
                    nameof(CursorEnd));
            }

            if (CursorEnd.Sequence < CursorStart.Sequence)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(CursorEnd),
                    CursorEnd,
                    "Build log end cursor must not precede the start cursor.");
            }
        }

        this.StartedAtUtc = validatedStartedAtUtc;
        this.CompletedAtUtc = validatedCompletedAtUtc;
        this.CursorStart = CursorStart;
        this.CursorEnd = CursorEnd;
    }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public IpcLogCursor? CursorStart { get; }

    public IpcLogCursor? CursorEnd { get; }
}
