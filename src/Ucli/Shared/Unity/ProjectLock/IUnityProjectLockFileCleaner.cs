namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Deletes a Unity lock file after stale ownership has been proven. </summary>
internal interface IUnityProjectLockFileCleaner
{
    /// <summary> Deletes one Unity lock file. </summary>
    /// <param name="lockFilePath"> The lock-file path to delete. </param>
    /// <returns> The cleanup result. </returns>
    UnityProjectLockFileCleanupResult Delete (string lockFilePath);
}
