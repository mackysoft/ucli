namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Represents the result of deleting one stale Unity lock file. </summary>
/// <param name="ErrorMessage"> The cleanup failure message when deletion failed. </param>
internal sealed record UnityProjectLockFileCleanupResult (string? ErrorMessage)
{
    /// <summary> Gets whether cleanup succeeded. </summary>
    public bool IsSuccess => ErrorMessage is null;

    /// <summary> Creates a successful cleanup result. </summary>
    /// <returns> The cleanup result. </returns>
    public static UnityProjectLockFileCleanupResult Success ()
    {
        return new UnityProjectLockFileCleanupResult((string?)null);
    }

    /// <summary> Creates a failed cleanup result. </summary>
    /// <param name="errorMessage"> The cleanup failure message. </param>
    /// <returns> The cleanup result. </returns>
    public static UnityProjectLockFileCleanupResult Failure (string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new UnityProjectLockFileCleanupResult(errorMessage);
    }
}
