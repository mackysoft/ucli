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
