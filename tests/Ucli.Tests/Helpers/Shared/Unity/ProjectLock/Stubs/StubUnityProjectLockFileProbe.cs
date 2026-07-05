using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal sealed class StubUnityProjectLockFileProbe : IUnityProjectLockFileProbe
{
    private readonly UnityProjectLockFileProbeResult result;

    public StubUnityProjectLockFileProbe (UnityProjectLockFileProbeResult result)
    {
        this.result = result;
    }

    public UnityProjectLockFileProbeResult Probe (string unityProjectRoot)
    {
        return result;
    }
}
