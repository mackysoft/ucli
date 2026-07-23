using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;

/// <summary> Represents one <c>logs * read</c> service execution result. </summary>
internal sealed record LogsReadServiceResult
{
    /// <summary> Initializes one result while enforcing the error and completion-reason contract. </summary>
    /// <param name="error"> The structured execution error when the read failed; otherwise <see langword="null" />. </param>
    /// <param name="count"> The number of entries emitted before completion. </param>
    /// <param name="nextCursor"> The latest cursor confirmed by the read flow. </param>
    /// <param name="completionReason"> The reason the read completed. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="count" /> is negative or <paramref name="completionReason" /> is undefined. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="error" /> and <paramref name="completionReason" /> do not describe the same outcome. </exception>
    public LogsReadServiceResult (
        ExecutionError? error,
        int count,
        string? nextCursor,
        LogsReadCompletionReason completionReason)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (!TextVocabulary.IsDefined(completionReason))
        {
            throw new ArgumentOutOfRangeException(nameof(completionReason), completionReason, "Log read completion reason must be specified.");
        }

        var hasError = error is not null;
        var isCanceledError = error?.Code == ExecutionErrorCodes.Canceled;
        var requiresError = completionReason is LogsReadCompletionReason.Error or LogsReadCompletionReason.Canceled;
        if (hasError != requiresError)
        {
            throw new ArgumentException("Log read error and completion reason must describe the same outcome.", nameof(error));
        }

        if (isCanceledError != (completionReason == LogsReadCompletionReason.Canceled))
        {
            throw new ArgumentException("Canceled log reads must use both the canceled error code and completion reason.", nameof(error));
        }

        Error = error;
        Count = count;
        NextCursor = nextCursor;
        CompletionReason = completionReason;
    }

    /// <summary> Gets the structured execution error when the read failed; otherwise <see langword="null" />. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets the number of entries emitted before completion. </summary>
    public int Count { get; }

    /// <summary> Gets the latest cursor confirmed by the read flow. </summary>
    public string? NextCursor { get; }

    /// <summary> Gets the reason the read completed. </summary>
    public LogsReadCompletionReason CompletionReason { get; }

    /// <summary> Gets a value indicating whether execution succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a normally completed service result. </summary>
    public static LogsReadServiceResult Completed (
        int count,
        string? nextCursor)
    {
        return new LogsReadServiceResult(null, count, nextCursor, LogsReadCompletionReason.Completed);
    }

    /// <summary> Creates a service result stopped by its idle timeout. </summary>
    public static LogsReadServiceResult IdleTimeout (
        int count,
        string? nextCursor)
    {
        return new LogsReadServiceResult(null, count, nextCursor, LogsReadCompletionReason.IdleTimeout);
    }

    /// <summary> Creates a service result stopped after reaching its inclusive upper timestamp bound. </summary>
    public static LogsReadServiceResult UntilReached (
        int count,
        string? nextCursor)
    {
        return new LogsReadServiceResult(null, count, nextCursor, LogsReadCompletionReason.UntilReached);
    }

    /// <summary> Creates a failed service result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed service result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static LogsReadServiceResult Failure (
        ExecutionError error,
        int count,
        string? nextCursor)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new LogsReadServiceResult(error, count, nextCursor, LogsReadCompletionReason.Error);
    }

    /// <summary> Creates a canceled service result with partial progress metadata. </summary>
    public static LogsReadServiceResult Canceled (
        int count,
        string? nextCursor)
    {
        return new LogsReadServiceResult(
            ExecutionError.InternalError("Log read was canceled.", ExecutionErrorCodes.Canceled),
            count,
            nextCursor,
            LogsReadCompletionReason.Canceled);
    }
}
