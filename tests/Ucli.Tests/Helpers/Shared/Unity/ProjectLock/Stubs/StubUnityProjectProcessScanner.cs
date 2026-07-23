using MackySoft.FileSystem;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal sealed class StubUnityProjectProcessScanner : IUnityProjectProcessScanner
{
    private readonly UnityProjectProcessScanResult result;

    public StubUnityProjectProcessScanner (UnityProjectProcessScanResult result)
    {
        this.result = result;
    }

    public ValueTask<UnityProjectProcessScanResult> FindProcessesForProjectAsync (
        AbsolutePath unityProjectRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(result);
    }
}
