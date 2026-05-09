namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Represents one Unity project lock preflight outcome. </summary>
/// <param name="Status"> The classified preflight status. </param>
/// <param name="LockFilePath"> The inspected Unity lock-file path when available. </param>
/// <param name="ProcessId"> The active owner process identifier when known. </param>
/// <param name="Message"> A diagnostic message for failure or cleanup outcomes. </param>
internal sealed record UnityProjectLockPreflightResult (
    UnityProjectLockPreflightStatus Status,
    string? LockFilePath,
    int? ProcessId,
    string? Message)
{
    /// <summary> Gets whether a Unity process startup may continue. </summary>
    public bool AllowsStartup => Status is UnityProjectLockPreflightStatus.Unlocked or UnityProjectLockPreflightStatus.StaleLockCleared;

    /// <summary> Creates an unlocked result. </summary>
    /// <param name="lockFilePath"> The inspected lock-file path. </param>
    /// <returns> The preflight result. </returns>
    public static UnityProjectLockPreflightResult Unlocked (string lockFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        return new UnityProjectLockPreflightResult(UnityProjectLockPreflightStatus.Unlocked, lockFilePath, null, null);
    }

    /// <summary> Creates an active-lock result. </summary>
    /// <param name="lockFilePath"> The Unity lock-file path. </param>
    /// <param name="processId"> The owner process identifier when known. </param>
    /// <param name="message"> The ownership diagnostic message. </param>
    /// <returns> The preflight result. </returns>
    public static UnityProjectLockPreflightResult ActiveLock (
        string lockFilePath,
        int? processId,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new UnityProjectLockPreflightResult(UnityProjectLockPreflightStatus.ActiveLock, lockFilePath, processId, message);
    }

    /// <summary> Creates a stale-lock-cleared result. </summary>
    /// <param name="lockFilePath"> The Unity lock-file path that was removed. </param>
    /// <returns> The preflight result. </returns>
    public static UnityProjectLockPreflightResult StaleLockCleared (string lockFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        return new UnityProjectLockPreflightResult(
            UnityProjectLockPreflightStatus.StaleLockCleared,
            lockFilePath,
            null,
            UnityProjectLockFailureMessage.CreateStaleLockCleared(lockFilePath));
    }

    /// <summary> Creates an ambiguous result. </summary>
    /// <param name="lockFilePath"> The Unity lock-file path. </param>
    /// <param name="message"> The ambiguity diagnostic message. </param>
    /// <returns> The preflight result. </returns>
    public static UnityProjectLockPreflightResult Ambiguous (
        string lockFilePath,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new UnityProjectLockPreflightResult(UnityProjectLockPreflightStatus.Ambiguous, lockFilePath, null, message);
    }

    /// <summary> Creates an inspection-failed result. </summary>
    /// <param name="message"> The inspection failure message. </param>
    /// <returns> The preflight result. </returns>
    public static UnityProjectLockPreflightResult InspectionFailed (string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new UnityProjectLockPreflightResult(UnityProjectLockPreflightStatus.InspectionFailed, null, null, message);
    }

    /// <summary> Creates a cleanup-failed result. </summary>
    /// <param name="lockFilePath"> The Unity lock-file path. </param>
    /// <param name="message"> The cleanup failure message. </param>
    /// <returns> The preflight result. </returns>
    public static UnityProjectLockPreflightResult CleanupFailed (
        string lockFilePath,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new UnityProjectLockPreflightResult(UnityProjectLockPreflightStatus.CleanupFailed, lockFilePath, null, message);
    }
}
