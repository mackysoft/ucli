using MackySoft.FileSystem;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Builds user-facing messages for Unity project lock failures. </summary>
internal static class UnityProjectLockFailureMessage
{
    /// <summary> Builds a message for a Unity project that is already open or locked. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="lockFilePath"> The Unity lock-file path when available. </param>
    /// <returns> The project-already-open message. </returns>
    public static string CreateAlreadyOpen (
        AbsolutePath unityProjectRoot,
        AbsolutePath? lockFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(unityProjectRoot);
        var lockFileSuffix = lockFilePath is null
            ? string.Empty
            : $" LockFile={lockFilePath.Value}";
        return $"Unity project is already open or locked by another Unity process. ProjectPath={unityProjectRoot.Value}.{lockFileSuffix}";
    }

    /// <summary> Builds a message for a Unity lock file whose ownership could not be decided safely. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="lockFilePath"> The Unity lock-file path. </param>
    /// <param name="reason"> The ambiguity reason. </param>
    /// <returns> The ambiguous lock message. </returns>
    public static string CreateAmbiguous (
        AbsolutePath unityProjectRoot,
        AbsolutePath? lockFilePath,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(unityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return $"Unity project lock-file ownership could not be determined safely. ProjectPath={unityProjectRoot.Value}. LockFile={lockFilePath?.Value ?? "<unknown>"}. {reason}";
    }

    /// <summary> Builds a message for stale Unity lock-file cleanup failure. </summary>
    /// <param name="lockFilePath"> The Unity lock-file path that could not be deleted. </param>
    /// <param name="reason"> The cleanup failure reason. </param>
    /// <returns> The cleanup failure message. </returns>
    public static string CreateCleanupFailed (
        AbsolutePath? lockFilePath,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return $"Stale Unity project lock file could not be removed. LockFile={lockFilePath?.Value ?? "<unknown>"}. {reason}";
    }

    /// <summary> Builds a diagnostic for a stale Unity lock file that uCLI removed. </summary>
    /// <param name="lockFilePath"> The removed Unity lock-file path. </param>
    /// <returns> The stale cleanup diagnostic message. </returns>
    public static string CreateStaleLockCleared (AbsolutePath lockFilePath)
    {
        ArgumentNullException.ThrowIfNull(lockFilePath);

        return $"Stale Unity project lock file was removed. LockFile={lockFilePath.Value}";
    }

    /// <summary> Builds a message for a lock-file state that could not be inspected. </summary>
    /// <param name="lockFilePath"> The Unity lock-file path that could not be inspected. </param>
    /// <param name="exception"> The exception raised while inspecting the path. </param>
    /// <returns> The lock-file inspection failure message. </returns>
    public static string CreateInspectionFailed (
        AbsolutePath lockFilePath,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(lockFilePath);
        ArgumentNullException.ThrowIfNull(exception);

        return $"Unity project lock-file state could not be inspected. LockFile={lockFilePath.Value}. {exception.Message}";
    }

}
