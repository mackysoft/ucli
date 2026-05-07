namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Represents the observed Unity project lock-file state. </summary>
/// <param name="IsLocked"> Whether Unity's lock file is present for the project. </param>
/// <param name="LockFilePath"> The lock-file path that was inspected when available. </param>
/// <param name="ErrorMessage"> The probe failure message when lock state could not be inspected. </param>
internal sealed record UnityProjectLockFileProbeResult (
    bool IsLocked,
    string? LockFilePath,
    string? ErrorMessage)
{
    /// <summary> Gets a value indicating whether the lock-file probe completed. </summary>
    public bool IsSuccess => ErrorMessage is null;

    /// <summary> Creates a result for an unlocked project. </summary>
    /// <param name="lockFilePath"> The lock-file path that was inspected. </param>
    /// <returns> The unlocked result. </returns>
    public static UnityProjectLockFileProbeResult Unlocked (string lockFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        return new UnityProjectLockFileProbeResult(false, lockFilePath, null);
    }

    /// <summary> Creates a result for a locked project. </summary>
    /// <param name="lockFilePath"> The lock-file path that exists. </param>
    /// <returns> The locked result. </returns>
    public static UnityProjectLockFileProbeResult Locked (string lockFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        return new UnityProjectLockFileProbeResult(true, lockFilePath, null);
    }

    /// <summary> Creates a result for a failed lock-state probe. </summary>
    /// <param name="errorMessage"> The probe failure message. </param>
    /// <returns> The failed result. </returns>
    public static UnityProjectLockFileProbeResult Failure (string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new UnityProjectLockFileProbeResult(false, null, errorMessage);
    }
}
