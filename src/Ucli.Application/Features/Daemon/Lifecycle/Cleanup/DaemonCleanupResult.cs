using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Represents the result of daemon cleanup operation. </summary>
internal sealed record DaemonCleanupResult
{
    private DaemonCleanupResult (
        DaemonCleanupStatus? status,
        DaemonCleanupSkipReason? skipReason,
        int deletedLaunchAttemptCount,
        ExecutionError? error)
    {
        Status = status;
        SkipReason = skipReason;
        DeletedLaunchAttemptCount = deletedLaunchAttemptCount;
        Error = error;
    }

    /// <summary> Gets the daemon cleanup outcome on success; otherwise <see langword="null" />. </summary>
    public DaemonCleanupStatus? Status { get; }

    /// <summary> Gets the cleanup skip reason when cleanup was skipped; otherwise <see langword="null" />. </summary>
    public DaemonCleanupSkipReason? SkipReason { get; }

    /// <summary> Gets the number of deleted launch-attempt directories. </summary>
    public int DeletedLaunchAttemptCount { get; }

    /// <summary> Gets the structured error when cleanup fails; otherwise <see langword="null" />. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether daemon cleanup succeeded. </summary>
    [MemberNotNullWhen(true, nameof(Status))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Status.HasValue;

    /// <summary> Creates a successful completed result. </summary>
    /// <param name="deletedLaunchAttemptCount"> The number of deleted launch-attempt directories. </param>
    /// <returns> The successful completed result. </returns>
    public static DaemonCleanupResult Completed (int deletedLaunchAttemptCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deletedLaunchAttemptCount);
        return new DaemonCleanupResult(DaemonCleanupStatus.Completed, skipReason: null, deletedLaunchAttemptCount, error: null);
    }

    /// <summary> Creates a successful skipped result. </summary>
    /// <param name="skipReason"> The cleanup skip reason. </param>
    /// <returns> The successful skipped result. </returns>
    public static DaemonCleanupResult Skipped (DaemonCleanupSkipReason skipReason)
    {
        if (!Enum.IsDefined(skipReason))
        {
            throw new ArgumentOutOfRangeException(nameof(skipReason), skipReason, "Cleanup skip reason must be defined.");
        }

        return new DaemonCleanupResult(DaemonCleanupStatus.Skipped, skipReason, 0, null);
    }

    /// <summary> Creates a failed cleanup result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed cleanup result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonCleanupResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonCleanupResult(status: null, skipReason: null, deletedLaunchAttemptCount: 0, error);
    }
}
