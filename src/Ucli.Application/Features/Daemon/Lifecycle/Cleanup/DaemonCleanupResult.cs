using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Represents the result of daemon cleanup operation. </summary>
/// <param name="Status"> The daemon cleanup outcome. </param>
/// <param name="SkipReason"> The cleanup skip reason when cleanup was skipped; otherwise <see cref="DaemonCleanupSkipReason.None" />. </param>
/// <param name="Error"> The structured error when cleanup fails; otherwise <see langword="null" />. </param>
internal sealed record DaemonCleanupResult (
    DaemonCleanupStatus Status,
    DaemonCleanupSkipReason SkipReason,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon cleanup succeeded. </summary>
    public bool IsSuccess => Status != DaemonCleanupStatus.Failed && Error is null;

    /// <summary> Creates a successful completed result. </summary>
    /// <returns> The successful completed result. </returns>
    public static DaemonCleanupResult Completed ()
    {
        return new DaemonCleanupResult(DaemonCleanupStatus.Completed, DaemonCleanupSkipReason.None, null);
    }

    /// <summary> Creates a successful skipped result. </summary>
    /// <param name="skipReason"> The cleanup skip reason. </param>
    /// <returns> The successful skipped result. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="skipReason" /> is <see cref="DaemonCleanupSkipReason.None" />. </exception>
    public static DaemonCleanupResult Skipped (DaemonCleanupSkipReason skipReason)
    {
        if (skipReason == DaemonCleanupSkipReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(skipReason), skipReason, "Cleanup skip reason must not be None.");
        }

        return new DaemonCleanupResult(DaemonCleanupStatus.Skipped, skipReason, null);
    }

    /// <summary> Creates a failed cleanup result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed cleanup result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonCleanupResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonCleanupResult(DaemonCleanupStatus.Failed, DaemonCleanupSkipReason.None, error);
    }
}
