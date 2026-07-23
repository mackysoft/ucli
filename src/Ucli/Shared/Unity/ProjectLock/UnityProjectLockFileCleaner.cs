using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Deletes stale Unity lock files after ownership has been ruled out. </summary>
internal sealed class UnityProjectLockFileCleaner : IUnityProjectLockFileCleaner
{
    /// <inheritdoc />
    public UnityProjectLockFileCleanupResult Delete (AbsolutePath lockFilePath)
    {
        ArgumentNullException.ThrowIfNull(lockFilePath);

        try
        {
            File.Delete(lockFilePath.Value);
            return UnityProjectLockFileCleanupResult.Success();
        }
        catch (FileNotFoundException)
        {
            return UnityProjectLockFileCleanupResult.Success();
        }
        catch (DirectoryNotFoundException)
        {
            return UnityProjectLockFileCleanupResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return UnityProjectLockFileCleanupResult.Failure(UnityProjectLockFailureMessage.CreateCleanupFailed(lockFilePath, exception.Message));
        }
    }
}
