using MackySoft.FileSystem;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal sealed class StubUnityProjectLockOwnerProbe : IUnityProjectLockOwnerProbe
{
    private readonly UnityProjectLockOwnerProbeResult result;

    public StubUnityProjectLockOwnerProbe (UnityProjectLockOwnerProbeResult result)
    {
        this.result = result;
    }

    public ValueTask<UnityProjectLockOwnerProbeResult> ProbeOwnerAsync (
        ResolvedUnityProjectContext unityProject,
        AbsolutePath lockFilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(result);
    }
}
