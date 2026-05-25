using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;

/// <summary> Represents one <c>logs * read</c> service execution result. </summary>
/// <param name="Error"> The structured execution error when command failed; otherwise <see langword="null" />. </param>
/// <param name="Count"> The number of entries emitted before completion. </param>
/// <param name="NextCursor"> The latest cursor confirmed by the read flow. </param>
/// <param name="CompletionReason"> The public completion reason. </param>
internal sealed record LogsReadServiceResult (
    ExecutionError? Error,
    int Count,
    string? NextCursor,
    string CompletionReason)
{
    /// <summary> Gets a value indicating whether execution succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful service result. </summary>
    /// <returns> The successful service result. </returns>
    public static LogsReadServiceResult Success (
        int count = 0,
        string? nextCursor = null,
        string completionReason = LogsReadCompletionReasons.Completed)
    {
        return new LogsReadServiceResult(
            Error: null,
            Count: count,
            NextCursor: nextCursor,
            CompletionReason: completionReason);
    }

    /// <summary> Creates a failed service result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed service result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static LogsReadServiceResult Failure (
        ExecutionError error,
        int count = 0,
        string? nextCursor = null,
        string completionReason = LogsReadCompletionReasons.Error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new LogsReadServiceResult(error, count, nextCursor, completionReason);
    }

    /// <summary> Creates a canceled service result with partial progress metadata. </summary>
    public static LogsReadServiceResult Canceled (
        int count = 0,
        string? nextCursor = null)
    {
        return Failure(
            ExecutionError.InternalError("Log read was canceled.", ExecutionErrorCodes.Canceled),
            count,
            nextCursor,
            LogsReadCompletionReasons.Canceled);
    }
}
