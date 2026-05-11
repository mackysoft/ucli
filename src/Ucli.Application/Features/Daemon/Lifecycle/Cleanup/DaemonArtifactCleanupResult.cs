using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Represents stale daemon artifact cleanup result. </summary>
internal sealed record DaemonArtifactCleanupResult (
    int DeletedLaunchAttemptCount,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether cleanup succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful cleanup result. </summary>
    /// <param name="deletedLaunchAttemptCount"> The number of deleted launch-attempt directories. </param>
    /// <returns> The cleanup result. </returns>
    public static DaemonArtifactCleanupResult Success (int deletedLaunchAttemptCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deletedLaunchAttemptCount);
        return new DaemonArtifactCleanupResult(deletedLaunchAttemptCount, null);
    }

    /// <summary> Creates a failed cleanup result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The cleanup result. </returns>
    public static DaemonArtifactCleanupResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonArtifactCleanupResult(0, error);
    }
}
