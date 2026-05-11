using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;

/// <summary> Represents a daemon launch-attempt store mutation result. </summary>
internal sealed record DaemonLaunchAttemptStoreOperationResult (
    int DeletedCount,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the store operation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful store operation result. </summary>
    /// <param name="deletedCount"> The number of deleted launch-attempt directories. </param>
    /// <returns> The store operation result. </returns>
    public static DaemonLaunchAttemptStoreOperationResult Success (int deletedCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deletedCount);
        return new DaemonLaunchAttemptStoreOperationResult(deletedCount, null);
    }

    /// <summary> Creates a failed store operation result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The store operation result. </returns>
    public static DaemonLaunchAttemptStoreOperationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonLaunchAttemptStoreOperationResult(0, error);
    }
}
