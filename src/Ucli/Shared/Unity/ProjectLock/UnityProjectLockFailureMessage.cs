namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Builds user-facing messages for Unity project lock failures. </summary>
internal static class UnityProjectLockFailureMessage
{
    /// <summary> Builds a message for a Unity project that is already open or locked. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="lockFilePath"> The Unity lock-file path when available. </param>
    /// <returns> The project-already-open message. </returns>
    public static string CreateAlreadyOpen (
        string unityProjectRoot,
        string? lockFilePath = null)
    {
        var lockFileSuffix = string.IsNullOrWhiteSpace(lockFilePath)
            ? string.Empty
            : $" LockFile={lockFilePath}";
        return $"Unity project is already open or locked by another Unity process. ProjectPath={unityProjectRoot}.{lockFileSuffix}";
    }

    /// <summary> Builds a message for a Unity lock file whose ownership could not be decided safely. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="lockFilePath"> The Unity lock-file path. </param>
    /// <param name="reason"> The ambiguity reason. </param>
    /// <returns> The ambiguous lock message. </returns>
    public static string CreateAmbiguous (
        string unityProjectRoot,
        string lockFilePath,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return $"Unity project lock-file ownership could not be determined safely. ProjectPath={unityProjectRoot}. LockFile={lockFilePath}. {reason}";
    }

    /// <summary> Builds a message for stale Unity lock-file cleanup failure. </summary>
    /// <param name="lockFilePath"> The Unity lock-file path that could not be deleted. </param>
    /// <param name="reason"> The cleanup failure reason. </param>
    /// <returns> The cleanup failure message. </returns>
    public static string CreateCleanupFailed (
        string lockFilePath,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return $"Stale Unity project lock file could not be removed. LockFile={lockFilePath}. {reason}";
    }

    /// <summary> Builds a diagnostic for a stale Unity lock file that uCLI removed. </summary>
    /// <param name="lockFilePath"> The removed Unity lock-file path. </param>
    /// <returns> The stale cleanup diagnostic message. </returns>
    public static string CreateStaleLockCleared (string lockFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);

        return $"Stale Unity project lock file was removed. LockFile={lockFilePath}";
    }

    /// <summary> Builds a message for a lock-file state that could not be inspected. </summary>
    /// <param name="lockFilePath"> The Unity lock-file path that could not be inspected. </param>
    /// <param name="exception"> The exception raised while inspecting the path. </param>
    /// <returns> The lock-file inspection failure message. </returns>
    public static string CreateInspectionFailed (
        string lockFilePath,
        Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        ArgumentNullException.ThrowIfNull(exception);

        return $"Unity project lock-file state could not be inspected. LockFile={lockFilePath}. {exception.Message}";
    }

}
