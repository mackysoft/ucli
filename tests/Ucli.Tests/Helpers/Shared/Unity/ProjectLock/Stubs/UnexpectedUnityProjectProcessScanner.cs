using MackySoft.FileSystem;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal sealed class UnexpectedUnityProjectProcessScanner : IUnityProjectProcessScanner
{
    public ValueTask<UnityProjectProcessScanResult> FindProcessesForProjectAsync (
        AbsolutePath unityProjectRoot,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Ambiguous EditorInstance ownership must not fall back to process scanning.");
    }
}
