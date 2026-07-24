using MackySoft.FileSystem;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal sealed class UnexpectedUnityProjectLockFileCleaner : IUnityProjectLockFileCleaner
{
    public UnityProjectLockFileCleanupResult Delete (AbsolutePath lockFilePath)
    {
        throw new InvalidOperationException("Active or ambiguous Unity project lock files must not be cleaned.");
    }
}
