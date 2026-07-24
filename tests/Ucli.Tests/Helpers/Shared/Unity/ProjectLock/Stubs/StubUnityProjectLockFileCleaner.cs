using MackySoft.FileSystem;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal sealed class StubUnityProjectLockFileCleaner : IUnityProjectLockFileCleaner
{
    private readonly UnityProjectLockFileCleanupResult result;

    public StubUnityProjectLockFileCleaner (UnityProjectLockFileCleanupResult result)
    {
        this.result = result;
    }

    public UnityProjectLockFileCleanupResult Delete (AbsolutePath lockFilePath)
    {
        return result;
    }
}
